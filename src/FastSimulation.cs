namespace Touchfish {
public sealed class FastSimulation {
 readonly MatchEngine game;

 internal FastSimulation(MatchEngine game){this.game=game;}

 public int ActiveSeat{get{return game.Active;}}
 public int Turn{get{return game.Turn;}}
 public int Winner{get{return game.Winner;}}
 public bool Finished{get{return game.Finished;}}
 public int TurnClockRemainingMs{get{return game.TurnClockRemainingMs;}}

 public SimulationSnapshot Capture(){return SimulationSnapshot.Capture(game);}
 public FastSimulation Fork(int? seed=null){return new FastSimulation(game.CopyForSimulation(seed));}
 public AgentObservation Observe(int seat){return AgentBridge.Observe(game,seat);}
 internal string ReuseObservationKey(AgentObservation view,SearchAction[] actions){return game.ReuseObservationKey(view,actions);}
 internal AgentObservation SearchView(int seat,bool moves){return AgentBridge.Observe(game,seat,false,moves);}
 public bool HasPendingDecision{get{return game.HasPendingDecision;}}
 public DecisionObservation ObserveDecision(int seat){return game.ObserveDecision(seat);}
 public ActionResult SubmitDecision(int seat,int decisionId,string optionId){
  try{return game.SubmitDecision(seat,decisionId,optionId);}
  finally{game.Log.Clear();}
 }

 public ActionResult Apply(int seat,AgentMove move){
  try{return AgentBridge.Execute(game,seat,move);}
  finally{game.Log.Clear();}
 }

 public ActionResult Mulligan(int seat,int[] handIds){
  try{return game.Mulligan(seat,handIds);}
  finally{game.Log.Clear();}
 }

 // A timeout may advance one turn. The next turn gets its own full timer.
 public bool AdvanceTime(int milliseconds){
  try{return game.AdvanceSimulationTime(milliseconds);}
  finally{game.Log.Clear();}
 }
}
}
