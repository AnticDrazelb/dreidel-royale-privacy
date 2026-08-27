using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DreidelRoyale.Audio;
using DreidelRoyale.Core;
using DreidelRoyale.UI;

namespace DreidelRoyale.Net
{
    /// <summary>
    /// The screens multiplayer adds: who's spinning, the room code, the lobby, and the
    /// overlay that appears when the host goes quiet.
    /// </summary>
    public class NetUI : MonoBehaviour
    {
        public UIManager UI;
        public GameController GC;
        public NetManager Net;

        RectTransform _reconnect, _observerChip;

        InputField _nameInput, _codeInput;
        Text _joinStatus, _lobbyCode, _lobbyStatus, _lobbyWait, _reconnectTitle,
             _reconnectMsg, _reconnectCountdown, _lobbyHint;
        Transform _lobbyList, _lobbyEnvPicker;
        Button _startBtn, _takeoverBtn;
        RectTransform _reconnectActions;

        string _pendingMode;     // "HOST" or "JOIN" - what the name screen leads into

        // ---------------------------------------------------------------
        public void Build(RectTransform root)
        {
            UI.MakeScreen("net-name", BuildName);
            UI.MakeScreen("net-code", BuildCode);
            UI.MakeScreen("net-lobby", BuildLobby);
            BuildReconnect(root);
            BuildObserverChip(root);
        }

        void BuildName(Transform c)
        {
            var h = UIKit.Label(c, "Who Spins?", 34, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 360, 46);
            var tag = UIKit.Label(c, "Pick your table name", 14, Theme.Sub);
            UIKit.SetSize(tag, 340, 24);

            // Practical advice rather than a warning: a phone that changes address mid-game
            // drops the link, and mobile data does that routinely.
            var note = UIKit.Panel(c, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.08f), 12f, "wifi-note");
            UIKit.Rect(note.gameObject).sizeDelta = new Vector2(340, 52);
            var noteT = UIKit.Label(note.transform,
                "Everyone needs to be on the <color=#f2c14e><b>same Wi-Fi</b></color>. "
                + "Mobile data can't find the table.", 12, Theme.Sub);
            UIKit.Stretch(noteT.gameObject, 12f);

            _nameInput = UIKit.Input(c, "YOUR NAME", 10);
            _nameInput.text = Store.Get("drdl-name") ?? "";

            UIKit.SectionLabel(c, "Your dreidel - earned through play");
            _nameSkinPicker = UIKit.Grid(c, new Vector2(96, 92)).transform;

            UIKit.Spacer(c, 6f);
            UIKit.Btn(c, "Continue", UIKit.BtnKind.Primary, ConfirmName);
            UIKit.Btn(c, "Back", UIKit.BtnKind.Ghost, () => UI.Show("landing"));
        }

        Transform _nameSkinPicker;

        void BuildCode(Transform c)
        {
            var h = UIKit.Label(c, "Room Code", 34, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 360, 46);
            var tag = UIKit.Label(c, "Ask the host for their 4 letters", 14, Theme.Sub);
            UIKit.SetSize(tag, 340, 24);

            _codeInput = UIKit.Input(c, "CODE", 15);

            UIKit.Btn(c, "Connect", UIKit.BtnKind.Primary, () =>
            {
                var code = RoomCode.Clean(_codeInput.text);
                if (!RoomCode.IsValid(code) && !LooksLikeAddress(code))
                {
                    _joinStatus.text = "Code is 4 letters";
                    return;
                }
                Sfx.Play("tick");
                _joinStatus.text = "Looking for the table...";
                Net.JoinGame(new LanTransport(), code, MyName());
            });

            _joinStatus = UIKit.Label(c, "", 12, Theme.Danger);
            UIKit.SetSize(_joinStatus, 340, 34);

            var hint = UIKit.Label(c,
                "On a network that blocks discovery, type the host's IP address instead of the code.",
                11, new Color(Theme.Sub.r, Theme.Sub.g, Theme.Sub.b, 0.75f));
            UIKit.SetSize(hint, 340, 30);

            UIKit.Btn(c, "Back", UIKit.BtnKind.Ghost, () => UI.Show("landing"));
        }

        static bool LooksLikeAddress(string s)
        {
            System.Net.IPAddress ip;
            return System.Net.IPAddress.TryParse(s, out ip);
        }

        void BuildLobby(Transform c)
        {
            UIKit.SectionLabel(c, "Room Code");
            _lobbyCode = UIKit.Label(c, "....", 46, Theme.Gold, TextAnchor.MiddleCenter, true);
            UIKit.SetSize(_lobbyCode, 340, 58);

            _lobbyHint = UIKit.Label(c, "", 11, new Color(Theme.Sub.r, Theme.Sub.g, Theme.Sub.b, 0.8f));
            UIKit.SetSize(_lobbyHint, 340, 30);

            _lobbyStatus = UIKit.Label(c, "Initialising...", 13, Theme.Sub);
            UIKit.SetSize(_lobbyStatus, 340, 24);

            var listGo = UIKit.Node("lobby-list", c);
            UIKit.Rect(listGo).sizeDelta = new Vector2(320, 40);
            var limg = listGo.AddComponent<Image>();
            limg.sprite = Theme.Rounded(Theme.RMd); limg.type = Image.Type.Sliced;
            limg.color = Theme.Surface1; limg.raycastTarget = false;
            var lv = listGo.AddComponent<VerticalLayoutGroup>();
            lv.spacing = 2f; lv.padding = new RectOffset(8, 8, 8, 8);
            lv.childForceExpandWidth = true; lv.childControlWidth = true;
            lv.childForceExpandHeight = false; lv.childControlHeight = false;
            listGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _lobbyList = listGo.transform;

            UIKit.SectionLabel(c, "Table - tap to vote");
            _lobbyEnvPicker = UIKit.Grid(c, new Vector2(96, 92)).transform;

            UIKit.Spacer(c, 6f);
            _startBtn = UIKit.Btn(c, "Start Game", UIKit.BtnKind.Primary, () =>
            {
                Sfx.Play("tick");
                Net.StartCountdown();
            });
            _lobbyWait = UIKit.Label(c, "Waiting for host...", 13, Theme.Sub);
            UIKit.SetSize(_lobbyWait, 340, 24);

            UIKit.Btn(c, "Leave Room", UIKit.BtnKind.Ghost, () =>
            {
                Sfx.Play("tick");
                Net.LeaveEverything();
                GC.IsLocalGame = true;
                UI.Show("landing");
            });
        }

        void BuildReconnect(RectTransform root)
        {
            var go = UIKit.Node("reconnect", root);
            _reconnect = UIKit.Stretch(go);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(Theme.Night.r, Theme.Night.g, Theme.Night.b, 0.94f);

            var col = UIKit.Node("col", go.transform);
            var crt = UIKit.Rect(col);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(340, 100);
            var v = col.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f; v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            v.childControlWidth = false; v.childControlHeight = false;
            col.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _reconnectTitle = UIKit.Label(col.transform, "Reconnecting", 32, Hex.To("#f4f6ff"),
                                          TextAnchor.MiddleCenter, true);
            UIKit.SetSize(_reconnectTitle, 320, 44);
            _reconnectMsg = UIKit.Label(col.transform, "Connection lost - trying to rejoin...", 14, Theme.Sub);
            UIKit.SetSize(_reconnectMsg, 320, 44);
            _reconnectCountdown = UIKit.Label(col.transform, "", 14, Theme.Gold,
                                              TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.SetSize(_reconnectCountdown, 320, 24);

            _takeoverBtn = UIKit.Btn(col.transform, "Continue Without Host", UIKit.BtnKind.Primary,
                                     () => { Sfx.Play("tick"); Net.ManualTakeover(); }, 240f);
            _takeoverBtn.gameObject.SetActive(false);

            var actions = UIKit.Node("actions", col.transform);
            _reconnectActions = UIKit.Rect(actions);
            var av = actions.AddComponent<VerticalLayoutGroup>();
            av.spacing = 8f; av.childAlignment = TextAnchor.MiddleCenter;
            av.childForceExpandWidth = false; av.childForceExpandHeight = false;
            av.childControlWidth = false; av.childControlHeight = false;
            actions.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIKit.Btn(actions.transform, "Try Again", UIKit.BtnKind.Primary,
                      () => { Sfx.Play("tick"); Net.TryReconnect(); }, 220f);
            UIKit.Btn(actions.transform, "Play Single Player", UIKit.BtnKind.Ghost, () =>
            {
                Net.LeaveEverything();
                GC.IsLocalGame = true;
                HideReconnect();
                UI.Show("cpu");
            }, 220f);
            UIKit.Btn(actions.transform, "Main Menu", UIKit.BtnKind.Ghost, () =>
            {
                Net.LeaveEverything();
                GC.IsLocalGame = true;
                HideReconnect();
                UI.BackToLanding();
            }, 220f);
            _reconnectActions.gameObject.SetActive(false);

            go.SetActive(false);
        }

        void BuildObserverChip(RectTransform root)
        {
            var go = UIKit.Node("observer-chip", root);
            _observerChip = UIKit.Rect(go);
            _observerChip.anchorMin = _observerChip.anchorMax = new Vector2(0.5f, 1f);
            _observerChip.pivot = new Vector2(0.5f, 1f);
            _observerChip.anchoredPosition = new Vector2(0, -56);
            _observerChip.sizeDelta = new Vector2(160, 30);
            var img = go.AddComponent<Image>();
            img.sprite = Theme.Rounded(15f); img.type = Image.Type.Sliced;
            img.color = new Color(20 / 255f, 27 / 255f, 58 / 255f, 0.92f);
            img.raycastTarget = false;
            UIKit.Border(go.transform, new Color(120 / 255f, 140 / 255f, 200 / 255f, 0.5f), 15f);
            var t = UIKit.Label(go.transform, "OBSERVING", 11, Theme.Sub,
                                TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Stretch(t.gameObject);
            go.SetActive(false);
        }

        // ---------------------------------------------------------------
        //  flow
        // ---------------------------------------------------------------
        public void BeginHost() { _pendingMode = "HOST"; UI.Show("net-name"); }
        public void BeginJoin() { _pendingMode = "JOIN"; UI.Show("net-name"); }

        public void OnNameScreenShown()
        {
            Pickers.RenderSkin(_nameSkinPicker, GC.MySkinChoice, id =>
            {
                GC.MySkinChoice = id;
                Store.Set("drdl-skin", id);
                GC.View.SetSkin(id);
                if (Net.Active && !Net.IsHost) Net.SendSkin(id);
                OnNameScreenShown();
            });
        }

        string MyName()
        {
            var n = _nameInput != null ? _nameInput.text.Trim() : "";
            return string.IsNullOrEmpty(n) ? "Player" : GameController.Trim(n, 10);
        }

        void ConfirmName()
        {
            var n = MyName();
            Store.Set("drdl-name", n);
            Sfx.Play("tick");
            if (_pendingMode == "HOST") Net.HostGame(new LanTransport(), n);
            else UI.Show("net-code");
        }

        // ---------------------------------------------------------------
        //  lobby
        // ---------------------------------------------------------------
        public void ShowLobby(string code, bool isHost, string waitText)
        {
            UI.Show("net-lobby");
            if (_lobbyCode != null) _lobbyCode.text = string.IsNullOrEmpty(code) ? "...." : code;
            if (_startBtn != null) _startBtn.gameObject.SetActive(isHost);
            if (_lobbyWait != null)
            {
                _lobbyWait.gameObject.SetActive(!isHost || !string.IsNullOrEmpty(waitText));
                _lobbyWait.text = waitText ?? "";
            }
            if (_lobbyHint != null)
            {
                // A host on a network that blocks broadcast can still be reached by address,
                // so the address is shown rather than left for someone to go hunting for.
                var addrs = isHost ? LanTransport.LocalAddresses() : new List<string>();
                _lobbyHint.text = addrs.Count > 0
                    ? "Same Wi-Fi as you. If the code won't find it, they can type " + addrs[0]
                    : "Everyone needs to be on the same Wi-Fi";
            }
            RefreshLobby();
        }

        public void SetLobbyStatus(string text, bool good)
        {
            if (_lobbyStatus == null) return;
            _lobbyStatus.text = "- " + text;
            _lobbyStatus.color = good ? Theme.Ok : Theme.Sub;
        }

        public void JoinFailed(string why)
        {
            if (_joinStatus != null) _joinStatus.text = why;
            if (UI.Current == "net-lobby") UI.Show("net-code");
        }

        public void RefreshLobby()
        {
            if (_lobbyList == null) return;
            UIKit.Clear(_lobbyList);

            var players = GC.G.Players;
            if (players.Count == 0)
            {
                var t = UIKit.Label(_lobbyList, "Waiting for players...", 13, new Color(0.32f, 0.36f, 0.55f));
                UIKit.SetSize(t, 300, 26);
                t.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            }

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                var row = UIKit.Row(_lobbyList, 8f, 34f, TextAnchor.MiddleLeft);
                UIKit.Rect(row).sizeDelta = new Vector2(296, 34);
                row.AddComponent<LayoutElement>().preferredHeight = 34;

                var av = UIKit.Node("avatar", row.transform);
                UIKit.Rect(av).sizeDelta = new Vector2(24, 24);
                var avImg = av.AddComponent<Image>();
                avImg.sprite = Theme.Circle();
                avImg.color = Consts.HueColor(i);
                var init = UIKit.Label(av.transform,
                    string.IsNullOrEmpty(p.Name) ? "?" : p.Name.Substring(0, 1).ToUpper(),
                    11, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                UIKit.Stretch(init.gameObject);

                string suffix = p.Id == "HOST" ? "  <color=#9aa3c7>host</color>"
                              : p.Disconnected ? "  <color=#ff5470>away</color>" : "";
                var n = UIKit.Label(row.transform, p.Name + suffix, 15, Theme.Text, TextAnchor.MiddleLeft);
                UIKit.SetSize(n, 220, 30);
            }

            Pickers.RenderEnv(_lobbyEnvPicker, GC.G.Env ?? GC.HostEnvChoice, id =>
            {
                Sfx.Play("tick");
                if (Net.IsHost) { GC.HostEnvChoice = id; GC.G.Env = id; GC.ApplyEnv(id); Net.Broadcast(); }
                else { GC.ApplyEnv(id); Net.SendVote(id); }
                RefreshLobby();
            });
        }

        // ---------------------------------------------------------------
        //  reconnect
        // ---------------------------------------------------------------
        public void ShowReconnect(string title, string message, bool canMigrate)
        {
            _reconnect.gameObject.SetActive(true);
            _reconnectTitle.text = title;
            _reconnectMsg.text = message;
            _reconnectCountdown.text = "";
            _takeoverBtn.gameObject.SetActive(canMigrate);
            _reconnectActions.gameObject.SetActive(false);
        }

        /// <summary>The link is not coming back: stop spinning and offer a way out.</summary>
        public void ShowReconnectDead(string message)
        {
            _reconnect.gameObject.SetActive(true);
            _reconnectTitle.text = "Disconnected";
            _reconnectMsg.text = message;
            _reconnectCountdown.text = "";
            _takeoverBtn.gameObject.SetActive(false);
            _reconnectActions.gameObject.SetActive(true);
        }

        public void SetReconnectMessage(string m) { if (_reconnectMsg != null) _reconnectMsg.text = m; }
        public void SetReconnectCountdown(string m) { if (_reconnectCountdown != null) _reconnectCountdown.text = m; }
        public void HideReconnect() { if (_reconnect != null) _reconnect.gameObject.SetActive(false); }
        public void ShowObserverChip(bool on) { if (_observerChip != null) _observerChip.gameObject.SetActive(on); }
    }
}
