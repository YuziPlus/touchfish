"""Reproducible GvG launch-neutral import; frozen old sets are never rewritten."""
from pathlib import Path
import json,hashlib,copy
from import_classic import BUILD,URL,plain,spell_damage
ROOT=Path(__file__).resolve().parents[1]
def main():
 raw=(ROOT/'work'/f'hsjson-{BUILD}-zhCN.json').read_bytes();source=json.loads(raw)
 cards=[c for c in source if c.get('set')=='GVG' and c.get('collectible') and c.get('cardClass')=='NEUTRAL']
 assert len(cards)==51
 helpers={'GVG_092t','GVG_110t','GVG_111t'}|{f'PART_00{i}' for i in range(1,8)}
 selected=cards+[c for c in source if c['id'] in helpers];assert len(selected)==61
 records=[]
 for c in selected:
  races=c.get('races',[c.get('race','')]);race=next((r for r in races if r in ('MECHANICAL','BEAST','DEMON','MURLOC','PIRATE','DRAGON','TOTEM')),'')
  r=dict(Id=c['id'],BaseId=c['id'],EffectId=c['id'],DbfId=c['dbfId'],LegacyDbfId=0,Name=c['name'],Text=plain(c.get('text')),RawText=c.get('text',''),CardClass=c.get('cardClass','NEUTRAL'),Type=c['type'],Rarity=c.get('rarity','FREE'),SetId='goblins-gnomes-2014',Cost=c.get('cost',0),Attack=c.get('attack',0),Health=c.get('health',0),Durability=c.get('durability',0),Collectible=c in cards,Mechanics=c.get('mechanics',[]),Race=race,Entourage=c.get('entourage',[]),Overload=c.get('overload',0),SpellDamage=spell_damage(c))
  if c['id']=='GVG_006':r.update(Cost=2,Attack=2,Health=3)
  if c['id']=='GVG_109':r.update(Cost=4,Attack=4)
  if c['id']=='GVG_114':r.update(Cost=8)
  if c['id']=='GVG_091':r.update(Text='嘲讽。无法成为法术或英雄技能的目标。',RawText='嘲讽。无法成为法术或英雄技能的目标。',Mechanics=['TAUNT','CANT_BE_TARGETED_BY_ABILITIES'])
  records.append(r)
 package=dict(SchemaVersion=1,Id='goblins-gnomes-2014',Name='地精大战侏儒 2014',DisplayName='地精大战侏儒',Version=f'GVG-launch-neutral-reconstructed-{BUILD}',Cards=sorted(records,key=lambda c:c['Id']))
 out=ROOT/'data/cardsets/goblins-gnomes-2014.json';out.write_text(json.dumps(package,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 extract=ROOT/'data/sources/goblins-gnomes-source.json';extract.write_text(json.dumps(selected,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 provenance=dict(SourceUrl=URL,SourceBuild=BUILD,SnapshotSha256=hashlib.sha256(raw).hexdigest(),SourceExtractSha256=hashlib.sha256(extract.read_bytes()).hexdigest(),PackageSha256=hashlib.sha256(out.read_bytes()).hexdigest(),DeckableCards=51,HelperCards=10,Cutoff='2014-12 launch values for new neutral cards only; no changes to frozen Classic/Naxx packages',Restored={'GVG_006':'2 mana 2/3, before 29.0','GVG_109':'4 mana 4/1, before Core 2021','GVG_114':'8 mana 5/7, before Core 2025','GVG_091':'2014 untargetable wording'},Sources=['https://hearthstone.blizzard.com/en-us/news/17076855','https://hearthstone.blizzard.com/en-us/news/16752637','https://hearthstone.blizzard.com/de-de/news/24064735/29-0-patch-notes','https://bnetcmsus-a.akamaihd.net/cms/content_entry_media/5L4LK5ASYDUL1739227664823.pdf'],Scope='51 new Neutral cards + seven Spare Parts, Chicken, Boom Bot and V-07-TR-0N. Random generation uses the match pool; unopened GvG class cards are not included yet.')
 (ROOT/'data/sources/goblins-gnomes-provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 p=ROOT/'data/pools.json';pools=json.loads(p.read_text(encoding='utf-8-sig'));base=next(x for x in pools['Pools'] if x['Id']=='naxxramas-2014');base['Archived']=True
 new=copy.deepcopy(base);new.update(Id='goblins-gnomes-2014',Name='地精大战侏儒·中立牌',Archived=False);new['CardSetIds'].append('goblins-gnomes-2014');pools['Pools']=[x for x in pools['Pools'] if x['Id']!=new['Id']]+[new];p.write_text(json.dumps(pools,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('Imported 51 GvG neutral cards and 10 helpers; active pool 463 cards')
if __name__=='__main__':main()
