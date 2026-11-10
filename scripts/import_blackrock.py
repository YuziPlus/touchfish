"""Frozen Blackrock Mountain collectible set and required tokens (2015 rules)."""
from pathlib import Path
import hashlib, json, re, urllib.request
ROOT=Path(__file__).resolve().parents[1]
URL='https://api.hearthstonejson.com/v1/25770/zhCN/cards.json'
SHA='4e104e20b3baff20f78dfc1e7ef07ba76628060cb073de778bfe1cb310cd9c1e'
def main():
    cache=ROOT/'work/brm-source-25770.json'
    raw=cache.read_bytes() if cache.exists() else urllib.request.urlopen(URL,timeout=60).read()
    assert hashlib.sha256(raw).hexdigest()==SHA,'Pinned source changed'
    helpers={'BRM_006t','BRM_010t','BRM_010t2','BRM_022t','BRM_027h','BRM_027p'}
    source=[c for c in json.loads(raw) if c['id'] in helpers or c.get('set')=='BRM' and c.get('collectible')]
    assert sum(bool(c.get('collectible')) for c in source)==31 and len(source)==37
    records=[]
    for c in source:
        text=c.get('text','')
        records.append(dict(Id=c['id'],BaseId=c['id'],EffectId=c['id'],DbfId=c['dbfId'],LegacyDbfId=0,Name=c['name'],Text=re.sub(r'<[^>]+>|[$#]','',text),RawText=text,CardClass=c.get('cardClass','NEUTRAL'),Type=c['type'],Rarity=c.get('rarity','FREE'),SetId='blackrock-mountain-2015',Cost=c.get('cost',0),Attack=c.get('attack',0),Health=c.get('health',0),Durability=c.get('durability',0),Collectible=bool(c.get('collectible')),Mechanics=c.get('mechanics',[]),Race=c.get('race',''),Entourage=c.get('entourage',[]),Overload=c.get('overload',0),SpellDamage=0))
    def write(p,d):(ROOT/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    write('data/cardsets/blackrock-mountain-2015.json',dict(SchemaVersion=1,Id='blackrock-mountain-2015',Name='黑石山的火焰·2015',DisplayName='黑石山的火焰',Version='2015-original-reconstructed-25770',Cards=records))
    write('data/sources/blackrock-source.json',source)
    write('data/sources/blackrock-provenance.json',dict(SourceUrl=URL,SourceBuild=25770,SourceSha256=SHA,Collectible=31,Helpers=6,HistoricalChecks=['Thaurissan 6 mana 5/5','Dark Iron Skulker 5 mana','Axe Flinger 2/5','Blackwing Corruptor deals 3'],Policy='Preserve existing frozen packages. Reconstruct original BRM cards, not a global 2015 balance patch.'))
    p=json.loads((ROOT/'data/pools.json').read_text(encoding='utf-8-sig'))
    old=next(x for x in p['Pools'] if x['Id']=='goblins-gnomes-2014')
    old['Archived']=True
    if not any(x['Id']=='blackrock-mountain-2015' for x in p['Pools']):
        new=dict(old,Id='blackrock-mountain-2015',Name='黑石山的火焰',Archived=False,CardSetIds=old['CardSetIds']+['blackrock-mountain-2015'])
        p['Pools'].append(new)
    write('data/pools.json',p)
    print('31 collectible + 6 helpers; active pool 566 cards')
if __name__=='__main__':main()
