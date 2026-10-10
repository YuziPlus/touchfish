using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Touchfish {
public enum GraveyardReason {MinionDied,SpellCast,Discarded,Burned,WeaponDestroyed,WeaponReplaced,ReturnOverflow}
// Match-local authoritative history. Never serialized into a LAN player view.
public sealed class GraveyardEntry {
 public readonly int Sequence,Turn,Seat,OriginalOwner,InstanceId,Attack,MaxHealth;
 public readonly string CardId,CardType;public readonly GraveyardReason Reason;
 internal GraveyardEntry(int sequence,int turn,int seat,int originalOwner,string id,string type,int instance,GraveyardReason reason,int attack,int health){Sequence=sequence;Turn=turn;Seat=seat;OriginalOwner=originalOwner;CardId=id;CardType=type;InstanceId=instance;Reason=reason;Attack=attack;MaxHealth=health;}
}
public sealed partial class MatchEngine {
 readonly List<GraveyardEntry> graveyard=new List<GraveyardEntry>();
 public ReadOnlyCollection<GraveyardEntry> Graveyard{get{return graveyard.AsReadOnly();}}
 public GraveyardEntry[] GraveyardFor(int seat,GraveyardReason? reason=null,string cardType=null){if(seat<0||seat>1)throw new ArgumentOutOfRangeException("seat");return graveyard.Where(e=>e.Seat==seat&&(!reason.HasValue||e.Reason==reason.Value)&&(cardType==null||e.CardType==cardType)).ToArray();}
 void RecordGraveyard(int seat,int originalOwner,string cardId,int instance,GraveyardReason reason,int attack=0,int health=0){graveyard.Add(new GraveyardEntry(graveyard.Count+1,Turn,seat,originalOwner,cardId,Card(cardId).Type,instance,reason,attack,health));}
}
}
