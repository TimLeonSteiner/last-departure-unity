using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const int WorldWindow=2,MovementWindow=3,MovementBytes=25;
        readonly Queue<int> pendingWorlds=new Queue<int>(),pendingMoves=new Queue<int>();
        int worldSequence,receivedWorldSequence,receivedInputSequence,lastReceiptSent;
        float receiptAt,lastWorldAt;
        long coopSentBytes;
        Vector3 coopCorrection;
        static int RemainingCoopBytes(FastBufferReader reader)=>reader.Length-reader.Position;
        static void Acknowledge(Queue<int> pending,int sequence)
        {while(pending.Count>0&&pending.Peek()<=sequence)pending.Dequeue();}
        void ResetCoopTraffic()
        {
            pendingWorlds.Clear();pendingMoves.Clear();worldSequence=receivedWorldSequence=receivedInputSequence=lastReceiptSent=0;
            receiptAt=lastWorldAt=0;coopSentBytes=0;coopCorrection=Vector3.zero;
        }
        void SendCoopReceipt(ulong destination,int sequence)
        {
            using(var writer=new FastBufferWriter(4,Allocator.Temp))
            {writer.WriteValueSafe(sequence);coop.CustomMessagingManager.SendNamedMessage("D27.Receipt",destination,writer,NetworkDelivery.ReliableFragmentedSequenced);coopSentBytes+=writer.Length;}
        }
        void ReceiveCoopReceipt(ulong id,FastBufferReader reader)
        {
            if(RemainingCoopBytes(reader)!=4)return;reader.ReadValueSafe(out int sequence);
            if (dedicatedServer)
            {
                if (sequence >= 0 && sequence <= worldSequence && dedicatedPending.TryGetValue(id,out var queue)) Acknowledge(queue,sequence);
                return;
            }
            if(IsCoopHost&&peer!=null&&id==peer.clientId&&sequence>=0&&sequence<=worldSequence)Acknowledge(pendingWorlds,sequence);
            else if(IsCoopGuest&&id==NetworkManager.ServerClientId&&sequence>=receivedInputSequence&&sequence<=coopSequence)
            {receivedInputSequence=sequence;Acknowledge(pendingMoves,sequence);}
        }
        static void WriteMovement(ref FastBufferWriter writer,CoopInput input)
        {
            writer.WriteValueSafe(input.sequence);writer.WriteValueSafe(input.yaw);writer.WriteValueSafe(input.pitch);
            writer.WriteValueSafe(input.movement.x);writer.WriteValueSafe(input.movement.y);writer.WriteValueSafe(input.skin);
            byte flags=(byte)((input.jump?1:0)|(input.slide?2:0)|(input.aim?4:0)|(input.dodge?8:0));writer.WriteValueSafe(flags);
        }
        static CoopInput ReadMovement(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int sequence);reader.ReadValueSafe(out float yaw);reader.ReadValueSafe(out float pitch);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out int skin);reader.ReadValueSafe(out byte flags);
            return new CoopInput{action="input",sequence=sequence,yaw=yaw,pitch=pitch,movement=new Vector2(x,y),skin=skin,jump=(flags&1)!=0,slide=(flags&2)!=0,aim=(flags&4)!=0,dodge=(flags&8)!=0};
        }
        void ReceiveCoopMovement(ulong id,FastBufferReader reader)
        {
            if (dedicatedServer)
            {
                if (RemainingCoopBytes(reader) != MovementBytes || !dedicatedPending.ContainsKey(id)) return;
                try { ReceiveDedicatedInput(id,ReadMovement(reader)); }
                catch(Exception e) { Debug.LogWarning("Rejected dedicated movement: " + e.Message); }
                return;
            }
            if(!IsCoopHost||peer==null||id!=peer.clientId||RemainingCoopBytes(reader)!=MovementBytes)return;
            try{ApplyCoopInput(ReadMovement(reader));}catch(Exception e){Debug.LogWarning("Rejected movement: "+e.Message);}
        }
        void SendCoopMovement(CoopInput input)
        {
            var writer=new FastBufferWriter(MovementBytes,Allocator.Temp);
            try{WriteMovement(ref writer,input);coop.CustomMessagingManager.SendNamedMessage("D27.Move",NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableFragmentedSequenced);coopSentBytes+=writer.Length;pendingMoves.Enqueue(input.sequence);}
            finally{writer.Dispose();}
        }
        void TickCoopCorrection(float dt)
        {
            if(dead){coopCorrection=Vector3.zero;return;}
            if(coopCorrection.sqrMagnitude<.0001f)return;
            Vector3 step=coopCorrection*(1-Mathf.Exp(-10*dt));
            Vector3 next=Move(player.position,step,PlayerRadius,true);next.y=player.position.y;
            Vector3 applied=next-player.position;player.position=next;coopCorrection-=step;
            foreach(var key in predictedCoopSequences)predictedCoopPositions[key]+=applied;
        }
    }
}
