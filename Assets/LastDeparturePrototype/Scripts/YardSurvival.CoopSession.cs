using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        NetworkManager coop;
        bool coopBusy,coopClosing;
        bool coopLobby,coopHostReady,coopGuestReady,coopStarted;
        string coopCode="",joinCode="",coopStatus="";
        float coopSendAt,coopSnapshotAt,coopInputAt,coopConnectDeadline;
        int coopAttempt;
        bool coopBackgroundOwned,coopPreviousBackground;
        bool IsCoopGuest=>coop!=null&&coop.IsClient&&!coop.IsHost&&coop.IsConnectedClient;
        bool IsCoopHost=>coop!=null&&coop.IsServer;
        bool HasCoopPartner=>IsCoopHost&&peer!=null&&peer.connected;
        // Retained entry points let old QA scripts fail explicitly without opening a connection.
        void HostCoop() => throw new NotSupportedException("This game is single-player only.");
        void JoinCoop() => throw new NotSupportedException("This game is single-player only.");
        void RegisterCoopMessages()
        {
            coop.CustomMessagingManager.RegisterNamedMessageHandler("D27.Input",ReceiveCoopInput);
            coop.CustomMessagingManager.RegisterNamedMessageHandler("D27.Move",ReceiveCoopMovement);
            coop.CustomMessagingManager.RegisterNamedMessageHandler("D27.Receipt",ReceiveCoopReceipt);
            coop.CustomMessagingManager.RegisterNamedMessageHandler("D27.World",ReceiveCoopWorld);
            coop.CustomMessagingManager.RegisterNamedMessageHandler("D27.Lobby",ReceiveCoopLobby);
        }
        void CoopConnected(ulong id)
        {
            if (dedicatedServer) { DedicatedConnected(id); return; }
            if(IsCoopHost&&id!=NetworkManager.ServerClientId){InitializePeer(id);if(!coopStarted)coopGuestReady=false;coopSnapshotAt=0;ShowBanner("Second player connected.");}
            else if(coop!=null&&!coop.IsHost&&id==coop.LocalClientId)
            {
                Debug.Log("Coop client connected; preparing lobby");
                coopBusy=false;EnterCoopLobby();coopStatus="Connected to host.";
                Debug.Log("Coop client lobby ready");
            }
        }
        void CoopDisconnected(ulong id)
        {
            if(coopClosing)return;
            if (dedicatedServer) { DedicatedDisconnected(id); return; }
            if(IsCoopHost&&peer!=null&&id==peer.clientId){peer.connected=false;coopHostReady=coopGuestReady=false;DestroyPeerAvatar();coopStatus="Your partner left the lobby.";ShowBanner(coopStarted?"Your partner left the session. You can keep playing.":coopStatus);}
            else if(coop!=null&&!coop.IsHost){string reason=coop.DisconnectReason;StopCoop();ShowHome();coopStatus=string.IsNullOrEmpty(reason)?"Disconnected from host.":reason;coopBusy=false;}
        }
        void StopCoop()
        {
            ++coopAttempt;coopClosing=true;DestroyPeerAvatar();peer=null;coopBusy=false;coopCode="";
            coopLobby=coopHostReady=coopGuestReady=coopStarted=false;
            ResetCoopTraffic();clientEpoch=-1;predictedCoopPositions.Clear();predictedCoopSequences.Clear();coopEnemyYaw.Clear();coopDefenseHp.Clear();
            if(coop!=null){var old=coop;coop=null;old.OnClientConnectedCallback-=CoopConnected;old.OnClientDisconnectCallback-=CoopDisconnected;old.Shutdown();Destroy(old.gameObject);}
            if(coopBackgroundOwned){Application.runInBackground=coopPreviousBackground;coopBackgroundOwned=false;}
            ClearDedicatedSession();
            coopClosing=false;
        }
        void SendCoopJson(string name,ulong destination,string json)
        {
            if(coop==null||!coop.IsListening||json.Length>50000)return;
            bool compact=name=="D27.World";
            if(compact)
            {
                byte[] bytes=System.Text.Encoding.UTF8.GetBytes(json);
                using(var writer=new FastBufferWriter(4+bytes.Length,Allocator.Temp))
                {writer.WriteValueSafe(bytes.Length);writer.WriteBytesSafe(bytes);coop.CustomMessagingManager.SendNamedMessage(name,destination,writer,NetworkDelivery.ReliableFragmentedSequenced);coopSentBytes+=writer.Length;}
            }
            else using(var writer=new FastBufferWriter(4+json.Length*2,Allocator.Temp))
            {writer.WriteValueSafe(json);coop.CustomMessagingManager.SendNamedMessage(name,destination,writer,NetworkDelivery.ReliableFragmentedSequenced);coopSentBytes+=writer.Length;}
        }
        void CoopPump()
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            if(coopBusy&&Time.unscaledTime>coopConnectDeadline){StopCoop();coopStatus="Connection timed out.";}
            if(IsCoopHost)
            {
                if(coopLobby){if(HasCoopPartner&&pendingWorlds.Count<WorldWindow&&Time.unscaledTime>=coopSnapshotAt){coopSnapshotAt=Time.unscaledTime+.25f;SendCoopLobby();}return;}
                UpdatePeer(dt);
                if(HasCoopPartner&&peer.lastSequence!=lastReceiptSent&&Time.unscaledTime>=receiptAt)
                {receiptAt=Time.unscaledTime+.05f;lastReceiptSent=peer.lastSequence;SendCoopReceipt(peer.clientId,lastReceiptSent);}
                if(HasCoopPartner&&(frontScreen!=FrontScreen.Game||dead)){UpdateFlashes(dt);TickWaveSimulation(dt);}
                if(HasCoopPartner&&pendingWorlds.Count<WorldWindow&&Time.unscaledTime>=coopSnapshotAt){coopSnapshotAt=Time.unscaledTime+.05f;SendCoopSnapshot();}
            }
            else if(IsCoopGuest)
            {
                if(coopLobby)return;
                TickCoopCorrection(dt);
                UpdateCoopEnemyVisuals(dt);
                CaptureCoopInput();
                if(Time.unscaledTime>=coopSendAt){coopSendAt=Time.unscaledTime+.033f;SendCoopCommand("input");}
            }
            UpdatePeerAvatar(dt);
        }

        void DrawCoopHud(float width)
        {
            if(coop==null||!coop.IsListening)return;
            EnsureDreadHud();
            DreadLabel(new Rect(32,116,240,20),"CO-OP / "+(IsCoopHost?"HOST":"PARTNER"),dreadSmall,DreadMuted);
            if(IsCoopGuest&&lastWorldAt>0&&Time.unscaledTime-lastWorldAt>.5f)
                DreadLabel(new Rect(32,138,260,24),"CONNECTION DELAY",dreadSmall,HudRed);
        }
    }
}
