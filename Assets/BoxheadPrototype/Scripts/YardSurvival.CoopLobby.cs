using System;
using Unity.Netcode;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        // Only the host decides when both ready players may begin the run.
        [Serializable] sealed class CoopLobbyState {public bool hostReady,guestReady,connected,started;}
        bool dedicatedOtherConnected;
        void EnterCoopLobby()
        {
            RestoreFrontEndEffects();ResetRun();coopLobby=true;coopStarted=false;
            coopHostReady=coopGuestReady=false;frontScreen=FrontScreen.Lobby;ReleaseMouse();
        }
        void SetCoopReady(bool ready)
        {
            if(!coopLobby)return;
            if(IsCoopHost){coopHostReady=ready;TryStartCoopLobby();SendCoopLobby();}
            else if(IsCoopGuest)SendCoopCommand("lobby-ready",type:ready?1:0);
        }
        void TryStartCoopLobby()
        {
            if(!IsCoopHost||!coopLobby||!HasCoopPartner||!coopHostReady||!coopGuestReady)return;
            coopLobby=false;coopStarted=true;PlayFromMenu();coopSnapshotAt=0;
        }
        void SendCoopLobby()
        {
            if(!HasCoopPartner)return;
            SendCoopJson("D27.Lobby",peer.clientId,JsonUtility.ToJson(new CoopLobbyState{
                hostReady=coopHostReady,guestReady=coopGuestReady,connected=true,started=coopStarted}));
        }
        void ReceiveCoopLobby(ulong id,FastBufferReader reader)
        {
            if(!IsCoopGuest||id!=NetworkManager.ServerClientId||reader.Length>1024)return;
            try
            {
                reader.ReadValueSafe(out string json);var state=JsonUtility.FromJson<CoopLobbyState>(json);
                if(state==null)return;
                coopHostReady=state.hostReady;coopGuestReady=state.guestReady;
                dedicatedOtherConnected=state.connected;
                coopStatus=state.started?"Starting game…":"Both players must be ready.";
            }
            catch(Exception e){Debug.LogWarning("Rejected malformed lobby state: "+e.Message);}
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void D27_CopyJoinCode(string code);
#endif
        void CopyCoopCode()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            D27_CopyJoinCode(coopCode);
#else
            GUIUtility.systemCopyBuffer=coopCode;
#endif
            coopStatus="Send code "+coopCode+" to your partner.";
        }
        void DrawCoopLobby(float width,float height)
        {
            float panel=Mathf.Min(520,width*.8f),left=(width-panel)/2,top=Mathf.Max(18,height*.06f);
            Fill(new Rect(0,0,width,height),new Color(.018f,.025f,.026f,.9f));
            var title=new GUIStyle(menuTitle){fontSize=44};
            var codeStyle=new GUIStyle(menuTitle){fontSize=42,alignment=TextAnchor.MiddleCenter};
            HudLabel(new Rect(left,top,panel,60),"CO-OP LOBBY",title,HudWhite);
            HudLabel(new Rect(left,top+62,panel,28),dedicatedConnection?"GAME SERVER · 2 PLAYERS":"JOIN CODE · 2 PLAYERS",menuSmall,HudWhite);
            HudLabel(new Rect(left,top+92,panel,56),coopCode,codeStyle,HudWhite);
            if(!dedicatedConnection&&MenuButton(new Rect(left,top+156,panel,42),"Copy code"))CopyCoopCode();
            HudLabel(new Rect(left,top+205,panel,42),dedicatedConnection?"Both players connect to the same server address. Sessions last up to 60 minutes.":"Share the code with your partner. They enter it in the main menu.",textStyle,HudWhite);
            bool connected=dedicatedConnection?dedicatedOtherConnected:IsCoopGuest||HasCoopPartner;
            HudLabel(new Rect(left,top+255,panel,30),(dedicatedConnection?"Partner":"Host"+(IsCoopHost?" (You)":""))+"     /     "+(!connected?"Waiting for player…":coopHostReady?"READY":"Not ready"),textStyle,HudWhite);
            HudLabel(new Rect(left,top+292,panel,30),"Partner"+(IsCoopGuest?" (You)":"")+"     /     "+(!connected?"Waiting for player…":coopGuestReady?"READY":"Not ready"),textStyle,HudWhite);
            bool ready=IsCoopHost?coopHostReady:coopGuestReady;
            if(MenuButton(new Rect(left,top+335,panel,48),ready?"Not ready":"Ready",true))SetCoopReady(!ready);
            HudLabel(new Rect(left,top+390,panel,42),!connected?"Waiting for your partner. No wave will start yet.":"The game starts automatically when both players are ready.",textStyle,HudWhite);
            if(MenuButton(new Rect(left,top+442,panel,40),"Leave lobby"))ShowHome();
            HudLabel(new Rect(left,top+488,panel,40),coopStatus,menuSmall,HudWhite);
        }
    }
}
