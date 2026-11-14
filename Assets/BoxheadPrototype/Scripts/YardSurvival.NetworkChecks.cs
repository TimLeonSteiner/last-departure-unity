#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunNetworkChecks()
        {
            var report=new StringBuilder();Action<bool,string> check=(ok,label)=>report.AppendLine((ok?"PASS ":"FAIL ")+label);
            PlayFromMenu();enabled=false;
            for(int flags=0;flags<8;flags++)
            {
                var input=new CoopInput{action="input",sequence=125+flags,yaw=-179.3f,pitch=74.5f,movement=new Vector2(-1,1),skin=1,jump=(flags&1)!=0,slide=(flags&2)!=0,aim=(flags&4)!=0};
                var writer=new FastBufferWriter(MovementBytes,Allocator.Temp);
                try
                {
                    WriteMovement(ref writer,input);
                    using(var reader=new FastBufferReader(writer,Allocator.Temp))
                    {
                        var decoded=ReadMovement(reader);
                        check(writer.Length==25&&decoded.sequence==input.sequence&&decoded.yaw==input.yaw&&decoded.pitch==input.pitch&&decoded.movement==input.movement&&decoded.skin==input.skin&&decoded.jump==input.jump&&decoded.slide==input.slide&&decoded.aim==input.aim,"Binary movement round trip, flags "+flags);
                    }
                    check(writer.Length<(.1f*(4+JsonUtility.ToJson(input).Length*2)),"Movement payload reduced by over 90%, flags "+flags);
                }
                finally{writer.Dispose();}
            }
            var envelope=new FastBufferWriter(64,Allocator.Temp);
            try
            {
                envelope.WriteValueSafe(123456UL);var input=new CoopInput{sequence=17,movement=Vector2.up};WriteMovement(ref envelope,input);
                using(var reader=new FastBufferReader(envelope,Allocator.Temp))
                {reader.ReadValueSafe(out ulong header);check(RemainingCoopBytes(reader)==MovementBytes&&ReadMovement(reader).sequence==17,"Payload validation excludes the already-consumed message header");}
            }
            finally{envelope.Dispose();}
            var pending=new Queue<int>();int sent=0;
            for(int tick=0;tick<1000;tick++)if(pending.Count<WorldWindow)pending.Enqueue(++sent);
            check(sent==2&&pending.Count==2,"A stalled guest permits only two world snapshots in flight");
            Acknowledge(pending,1);check(pending.Count==1&&pending.Peek()==2,"Acknowledging one snapshot frees exactly one slot");
            Acknowledge(pending,0);check(pending.Count==1,"Old acknowledgements cannot remove newer snapshots");
            Acknowledge(pending,2);check(pending.Count==0,"All acknowledged snapshots leave the window");
            for(int i=1;i<=3;i++)pendingMoves.Enqueue(i);Acknowledge(pendingMoves,2);
            check(pendingMoves.Count==1&&pendingMoves.Peek()==3,"Movement receipts advance independently of world snapshots");
            peer=new PeerPlayer{health=100,ammo=0,reserve=10};coopLobby=false;
            ApplyCoopInput(new CoopInput{action="input",sequence=10,movement=Vector2.right,jump=true});
            ApplyCoopInput(new CoopInput{action="input",sequence=9,movement=Vector2.left});
            check(peer.lastSequence==10&&peer.movement==Vector2.right&&peer.jump,"Stale movement cannot replace current input or erase jump");
            ApplyCoopInput(new CoopInput{action="reload",sequence=11});float reload=peer.reload;
            ApplyCoopInput(new CoopInput{action="reload",sequence=11});
            check(reload>0&&peer.reload==reload&&peer.lastSequence==11,"Reliable commands apply once alongside compact movement");
            ApplyCoopInput(new CoopInput{action="input",sequence=12,yaw=float.NaN});
            check(peer.lastSequence==11,"Non-finite movement is rejected without advancing sequence");
            string sample=JsonUtility.ToJson(new CoopWorldState{sequence=1,host=new CoopPlayerState{position=Vector3.one},guest=new CoopPlayerState(),spentBarrels=new[]{"Assets/Boxhead.unity/0","réserve"}});
            byte[] bytes=Encoding.UTF8.GetBytes(sample);
            check(Encoding.UTF8.GetString(bytes)==sample&&JsonUtility.FromJson<CoopWorldState>(Encoding.UTF8.GetString(bytes)).spentBarrels[1]=="réserve","Compact world encoding preserves all values and Unicode");
            check(bytes.Length<sample.Length*1.1f,"Typical world payload is approximately half the previous UTF-16 size");
            predictedCoopPositions.Clear();predictedCoopSequences.Clear();RememberCoopPosition(20);Vector3 before=player.position;
            ReconcileCoopPosition(20,before+Vector3.right);check(player.position==before&&coopCorrection==Vector3.right,"Small reconciliation error does not instantly move the camera");
            TickCoopCorrection(.016f);check(Vector3.Distance(before,player.position)>0&&Vector3.Distance(before,player.position)<.3f,"Prediction correction is spread across frames");
            ResetCoopTraffic();check(pendingMoves.Count==0&&pendingWorlds.Count==0&&coopCorrection==Vector3.zero,"New session clears traffic and correction state");peer=null;
            return report.ToString();
        }
    }
}
#endif
