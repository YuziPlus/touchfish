"""Validate and stream P5 self-play examples with Python's standard library."""
import argparse
import gzip
import hashlib
import json
from collections import Counter
from pathlib import Path

PLAYER = set("""
Health Armor Mana MaxMana DeckCount HandCount HeroAttack HeroMaxHealth Fatigue
SecretCount WeaponAttack WeaponDurability HeroAttacks WeaponWindfury ClassId
HeroFormId HeroAbilityId HeroPowerTarget WeaponId HeroPowerCost Shadowform
LockedMana Overload NextDragonDiscount Frozen Immune PowerUsed Board
""".split())
UNIT = set("""
Id Attack Health MaxHealth SpellDamage AttacksRemaining Cost SummonTurn AttacksUsed
AttackLimit CardId Key Race Charge AbilityTargetProtected Frozen Shield Taunt
Silenced Stealth Poison Windfury Immune
""".split())
OBSERVATION = set("""
Seat Turn FirstSeat ActiveSeat CardPoolId BoardSlots HandLimit ManaLimit
MulliganComplete CanAct SpellPower PowerCost PowerDamage CardsPlayed DeathsThisTurn
PreparationDiscount Self Enemy Hand OwnSecrets Decision own_initial_deck history
""".split())
HAND = set("Id Cost BaseCost UndiscountedCost CardId Key Type Charge Secret".split())
HISTORY = set("Sequence Seat Count Adjustment VisibleTo Kind CardId".split())
ACTION = set("""
key kind source_zone source_card target_side target_zone target_card choice
source_slot target_slot position option_index visits replace_slots value
""".split())
ROW = set("""
schema_version game_id game_seed seat turn decision_index player_class model
search_config mulligan_samples observation legal_actions selected_action search z final
""".split())


def require(condition, message):
    if not condition:
        raise ValueError(message)


def policy_target(actions):
    """Return policy probabilities or None when there are no root visits."""
    visits = [action["visits"] or 0 for action in actions]
    total = sum(visits)
    return [visit / total for visit in visits] if total else None


def split_for_seed(seed, validation_percent=10):
    """Initiative pairs share their seed and therefore their split."""
    digest = hashlib.sha256(str(seed).encode("ascii")).digest()
    return "validation" if int.from_bytes(digest[:8], "big") % 100 < validation_percent else "train"


def validate_action(action, observation):
    require(set(action) == ACTION, "Unexpected candidate fields")
    source = action["source_zone"]
    if source in ("hand", "board"):
        units = observation["Hand"] if source == "hand" else observation["Self"]["Board"]
        slot = action["source_slot"]
        require(type(slot) is int and 0 <= slot < len(units), "Invalid source slot")
        require(action["source_card"] == units[slot]["CardId"], "Invalid source card")
    if action["target_zone"] == "board":
        require(action["target_side"] in ("self", "enemy"), "Invalid target side")
        side = "Self" if action["target_side"] == "self" else "Enemy"
        board = observation[side]["Board"]
        slot = action["target_slot"]
        require(type(slot) is int and 0 <= slot < len(board), "Invalid target slot")
        require(action["target_card"] == board[slot]["CardId"], "Invalid target card")
    if action["position"] is not None:
        require(0 <= action["position"] <= len(observation["Self"]["Board"]), "Invalid summon position")
    if action["kind"] == "mulligan":
        slots = action["replace_slots"]
        require(slots == sorted(set(slots)) and all(0 <= i < len(observation["Hand"]) for i in slots), "Invalid mulligan subset")
        require(action["key"] == "mulligan:" + str(sum(1 << i for i in slots)), "Invalid mulligan key")
    if action["kind"] == "decision":
        option = action["option_index"]
        options = observation["Decision"]["Options"]
        require(type(option) is int and 0 <= option < len(options), "Invalid dynamic option")
        require(action["choice"] == options[option]["Id"], "Dynamic option mismatch")
    require(action["visits"] is None or type(action["visits"]) is int and action["visits"] >= 0, "Invalid visits")


def validate_row(row, game, index):
    require(set(row) == ROW and row["schema_version"] == 1, "Unexpected row schema")
    require(row["game_id"] == game["GameId"] and row["game_seed"] == game["Seed"], "Game identity mismatch")
    seat, winner = row["seat"], game["Winner"]
    require(row["decision_index"] == index and seat in (0, 1) and winner in (-1, 0, 1), "Invalid row index, seat or winner")
    require(row["z"] == (0 if winner < 0 else 1 if seat == winner else -1), "Incorrect label perspective")
    require(row["final"]["winner"] == winner and row["final"]["turns"] == game["Turns"], "Final result mismatch")
    observation = row["observation"]
    require(set(observation) == OBSERVATION and observation["Seat"] == seat, "Unexpected observation fields or seat")
    require(observation["FirstSeat"] == game["FirstSeat"] and row["player_class"] == observation["Self"]["ClassId"], "Incorrect initiative or class")
    for side in ("Self", "Enemy"):
        player = observation[side]
        require(set(player) == PLAYER and all(set(unit) == UNIT for unit in player["Board"]), "Unexpected public player or unit fields")
    require(all(set(card) == HAND for card in observation["Hand"]), "Unexpected hand fields")
    require(len(observation["Hand"]) == observation["Self"]["HandCount"], "Own hand count mismatch")
    require(all(set(card) == {"card_id", "count"} for card in observation["own_initial_deck"]), "Unexpected own deck fields")
    for event in observation["history"]:
        require(set(event) == HISTORY and event["VisibleTo"] in (-1, seat), "Private or unexpected history event")
        if event["Kind"] == 9:  # HistoryKind.ArmSecret
            require(event["CardId"] is None, "Secret identity exposed at arm time")
    decision = observation["Decision"]
    if decision is not None:
        require(set(decision) == {"Id", "Seat", "Prompt", "Options"} and decision["Seat"] == seat, "Opponent's private decision exposed")
        require(all(set(option) == {"Id", "Title", "Description", "Cost"} for option in decision["Options"]), "Unexpected private option fields")
    actions = row["legal_actions"]
    keys = {action["key"] for action in actions}
    require(len(keys) == len(actions) and row["selected_action"] in keys, "Duplicate or missing legal action")
    for action in actions:
        validate_action(action, observation)
    if row["search"]["mode"] == "forced":
        require(len(actions) == 1 and actions[0]["visits"] is None and row["search"]["iterations"] == 0, "Fabricated forced-action statistics")


def iter_games(directory):
    """Yield (manifest entry, decision rows) after checksum and schema checks."""
    directory = Path(directory)
    manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
    require(manifest["schema_version"] == 1, "Unsupported manifest version")
    seen = set()
    for game in manifest["games"]:
        game_id, name = game["GameId"], game["File"]
        require(game_id not in seen and name in (game_id + ".jsonl", game_id + ".jsonl.gz") and Path(name).name == name, "Duplicate or invalid shard")
        seen.add(game_id)
        path = directory / "shards" / name
        payload = path.read_bytes()
        require(len(payload) == game["Bytes"] and hashlib.sha256(payload).hexdigest() == game["Sha256"], "Shard checksum mismatch")
        if name.endswith(".gz"):
            payload = gzip.decompress(payload)
        rows = [json.loads(line) for line in payload.splitlines()]
        require(len(rows) == game["Samples"] and len(rows) >= 3, "Incorrect sample count")
        for index, row in enumerate(rows):
            validate_row(row, game, index)
        require([row["seat"] for row in rows[:2]] == [0, 1] and all(row["search"]["mode"] == "mulligan" for row in rows[:2]), "Missing opening decisions")
        yield game, rows


def examples(directory, validation_percent=10):
    """Yield split, observation, candidates, selected index, policy target, z."""
    for game, rows in iter_games(directory):
        for row in rows:
            actions = row["legal_actions"]
            index = next(i for i, action in enumerate(actions) if action["key"] == row["selected_action"])
            yield split_for_seed(game["Seed"], validation_percent), row["observation"], actions, index, policy_target(actions), row["z"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    counts, types = Counter(), Counter()
    for game, rows in iter_games(args.directory):
        counts["games"] += 1
        counts["samples"] += len(rows)
        counts[split_for_seed(game["Seed"])] += len(rows)
        for row in rows:
            selected = next(action for action in row["legal_actions"] if action["key"] == row["selected_action"])
            types[selected["kind"]] += 1
            counts["policy_targets"] += policy_target(row["legal_actions"]) is not None
    print(json.dumps({"counts": counts, "selected_action_types": types}, indent=2))


if __name__ == "__main__":
    main()
