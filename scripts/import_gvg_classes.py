"""Reproduce GvG class package; reuse upstream's neutral cards and Spare Parts."""
from pathlib import Path
import json, hashlib, re, urllib.request
ROOT=Path(__file__).resolve().parents[1]
URL='https://api.hearthstonejson.com/v1/25770/zhCN/cards.json'
def main():
    cache=ROOT/'work/gvg-source.json'
    raw=cache.read_bytes() if cache.exists() else urllib.request.urlopen(URL,timeout=60).read()
    assert hashlib.sha256(raw).hexdigest()=='4e104e20b3baff20f78dfc1e7ef07ba76628060cb073de778bfe1cb310cd9c1e', 'Pinned source changed'
    source=json.loads(raw);helpers={'GVG_028t','GVG_056t','GVG_080t','GVG_045t'}
    selected=[c for c in source if c['id'] in helpers or c.get('set')=='GVG' and c.get('collectible') and c.get('cardClass')!='NEUTRAL']
    assert len(selected)==76
    records=[]
    for c in selected:
        text=c.get('text','')
        if c['id']=='GVG_018':text='每当该随从造成伤害，为你的英雄恢复等量的生命值。'
        if c['id']=='GVG_072':text='每当一个角色获得治疗，便对一个随机敌人造成1点伤害。'
        records.append(dict(Id=c['id'],BaseId=c['id'],EffectId=c['id'],DbfId=c['dbfId'],LegacyDbfId=0,Name=c['name'],Text=re.sub(r'<[^>]+>|[$#]','',text),RawText=text,CardClass=c.get('cardClass','NEUTRAL'),Type=c['type'],Rarity=c.get('rarity','FREE'),SetId='gvg-2014-classes',Cost=c.get('cost',0),Attack=c.get('attack',0),Health=c.get('health',0),Durability=c.get('durability',0),Collectible=bool(c.get('collectible')),Mechanics=[m for m in c.get('mechanics',[]) if m!='LIFESTEAL'],Race='' if c.get('race')=='ELEMENTAL' else c.get('race',''),Entourage=c.get('entourage',[]),Overload=c.get('overload',0),SpellDamage=1 if c['id']=='GVG_123' else 0))
    def write(path,data):(ROOT/path).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    write('data/cardsets/gvg-2014-classes.json',dict(SchemaVersion=1,Id='gvg-2014-classes',Name='地精大战侏儒·职业卡',DisplayName='地精大战侏儒·职业卡',Version='2014-reconstructed-25770',Cards=records))
    write('data/sources/gvg-class-source.json',selected)
    write('data/sources/gvg-class-provenance.json',dict(SourceUrl=URL,SourceBuild=25770,SourceSha256=hashlib.sha256(raw).hexdigest(),Collectible=72,Helpers=4,ReusedUpstreamHelpers=7,Scope='72 class cards added to upstream neutral implementation; no duplicate Parts or neutral IDs',Reconstruction=['Removed later Elemental tag','Restored Shadowboxer character healing before 2018 nerf','Restored native Mistress of Pain healing instead of modern Lifesteal']))
    path=ROOT/'data/pools.json';pools=json.loads(path.read_text(encoding='utf-8-sig'));active=next(p for p in pools['Pools'] if p['Id']=='goblins-gnomes-2014')
    if 'gvg-2014-classes' not in active['CardSetIds']:active['CardSetIds'].append('gvg-2014-classes')
    active['Name']='地精大战侏儒';write('data/pools.json',pools)
    print('72 class cards + 4 class helpers; reused 51 neutral cards and 10 neutral helpers; 535 collectible cards')
if __name__=='__main__':main()
