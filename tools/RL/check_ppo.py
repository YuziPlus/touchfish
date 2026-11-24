"""Small deterministic test of grouped sampling and transactional KL rejection."""
import numpy as np
import torch
from torch import nn
from iterate import Model, batches
from ppo import grouped_order, optimize

torch.set_num_threads(1)
torch.manual_seed(44)
rng=np.random.default_rng(44)
groups=np.repeat(np.arange(8),4).tolist()
order=list(grouped_order(groups,rng))
assert sorted(order)==list(range(32))
assert len({groups[i] for i in order[:8]})==8
actor=Model()
critic=nn.Linear(293,1)
x=np.ones(293,np.float32)
actions=np.zeros((3,63),np.float32)
actions[:,0]=[0,1,2]
rows=[(x,actions,np.array([1,0,0],np.float32),.25) for _ in groups]
with torch.no_grad():
    p=(actor(torch.from_numpy(x[None]),torch.from_numpy(actions[None]))/1.1)[0].double().softmax(0).tolist()
before=[v.detach().clone() for v in actor.parameters()]
r, opt, _=optimize(actor,critic,rows,[1.]*32,groups,[p]*32,batches,22,1.1,.01,max_kl=1e-10,actor_batch=8,critic_batch=8)
assert r["actor_updates"]==0 and r["actor_attempts"]==1 and r["critic_updates"]==4
assert not opt.state and all(torch.equal(a,b) for a,b in zip(before,actor.parameters()))
assert r["actor"][0]["kl"]>1e-10 and r["final"]["kl"]<1e-10
print("PASS grouped minibatches and transactional KL rollback")
