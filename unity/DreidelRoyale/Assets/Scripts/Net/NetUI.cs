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

        string _pendingMode;     // "HOST", "JOIN" or "QUICK" - what the name screen leads into
        Text _quickStatus;

        Transform _modeRow, _lobbyRulesPicker, _lobbyAntePicker;
        Text _lobbyAnteLabel;
        Text _modeNote, _codeTag, _codeHint, _nameTag;

        /// <summary>
        /// Online (relay) or same-Wi-Fi (sockets). Remembered between runs, because whichever
        /// one works for a given group of friends tends to keep working for them.
        /// </summary>
        bool _online = true;

        // ---------------------------------------------------------------
        public void Build(RectTransform root)
        {
            UI.MakeScreen("net-name", BuildName);
            UI.MakeScreen("net-code", BuildCode);
            UI.MakeScreen("net-lobby", BuildLobby);
            UI.MakeScreen("net-quick", BuildQuick);
            BuildReconnect(root);
            BuildObserverChip(root);
        }

        void BuildName(Transform c)
        {
            var h = UIKit.Label(c, "Who Spins?", 34, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 360, 46);
            _nameTag = UIKit.Label(c, "Pick your table name", 14, Theme.Sub);
            UIKit.SetSize(_nameTag, 340, 24);

            _nameInput = UIKit.Input(c, "YOUR NAME", 10);

            UIKit.SectionLabel(c, "How you're connecting");
            _modeRow = UIKit.Row(c).transform;

            // The note under the picker is the whole of the explanation either mode needs,
            // and it is practical rather than a warning: a phone that changes address
            // mid-game drops a Wi-Fi table, and mobile data does that routinely.
            var note = UIKit.Panel(c, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.08f), 12f, "mode-note");
            UIKit.Rect(note.gameObject).sizeDelta = new Vector2(340, 52);
            _modeNote = UIKit.Label(note.transform, "", 12, Theme.Sub);
            UIKit.Stretch(_modeNote.gameObject, 12f);
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
            _codeTag = UIKit.Label(c, "Ask the host for their room code", 14, Theme.Sub);
            var tag = _codeTag;
            UIKit.SetSize(tag, 340, 24);

            _codeInput = UIKit.Input(c, "CODE", 15);

            UIKit.Btn(c, "Connect", UIKit.BtnKind.Primary, () =>
            {
                var code = RoomCode.Clean(_codeInput.text);
                if (!RoomCode.IsValid(code) && !(!_online && LooksLikeAddress(code)))
                {
                    _joinStatus.text = _online ? "That code doesn't look right"
                                               : "Code is 4 letters";
                    return;
                }
                Sfx.Play("tick");
                _joinStatus.text = _online ? "Connecting..." : "Looking for the table...";

                // An IP address is a Wi-Fi-only escape hatch, so typing one picks that route
                // regardless of the switch - it can't mean anything else.
                Net.JoinGame(LooksLikeAddress(code) ? new LanTransport() : MakeTransport(),
                             code, MyName());
            });

            _joinStatus = UIKit.Label(c, "", 12, Theme.Danger);
            UIKit.SetSize(_joinStatus, 340, 34);

            _codeHint = UIKit.Label(c,
                "On a network that blocks discovery, type the host's IP address instead of the code.",
                11, new Color(Theme.Sub.r, Theme.Sub.g, Theme.Sub.b, 0.75f));
            UIKit.SetSize(_codeHint, 340, 30);

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

            UIKit.Btn(c, "Share Invite", UIKit.BtnKind.Ghost, ShareInvite, 180f, 42f, 14);

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

            UIKit.SectionLabel(c, "Game style - host sets");
            _lobbyRulesPicker = UIKit.Row(c, 6f, 44f).transform;
            _lobbyAnteLabel = UIKit.SectionLabel(c, "Starting ante");
            _lobbyAntePicker = UIKit.Row(c, 6f, 44f).transform;

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
                Sfx.Play("tick"); Sfx.Buzz(10);
                // Same teardown as quitting a game: tell anyone connected before going.
                if (Net.IsHost && Net.PlayerCount > 1) UI.Toast("Closing the table...");
                Net.LeaveEverything();
                GC.IsLocalGame = true;
                UI.Show("landing");
            });
        }

        void BuildQuick(Transform c)
        {
            var h = UIKit.Label(c, "Quick Match", 34, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 360, 46);
            _quickStatus = UIKit.Label(c, "Searching for open tables...", 14, Theme.Sub);
            UIKit.SetSize(_quickStatus, 340, 40);

            var spinner = UIKit.Node("spinner", c);
            UIKit.Rect(spinner).sizeDelta = new Vector2(48, 48);
            var img = spinner.AddComponent<Image>();
            img.sprite = Theme.Ring(48, 4f);
            img.color = Theme.Gold;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillAmount = 0.25f;
            img.raycastTarget = false;
            spinner.AddComponent<Spin>();

            UIKit.Spacer(c, 10f);
            UIKit.Btn(c, "Cancel", UIKit.BtnKind.Ghost, () =>
            {
                Sfx.Play("tick");
                Net.CancelQuickMatch();
                UI.Show("landing");
            });
        }

        public void SetQuickStatus(string text)
        {
            if (_quickStatus != null) _quickStatus.text = text;
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
        /// <summary>
        /// An invite arriving from outside the app: the code is known before the name is, so
        /// the name screen is told what it is leading into and says so.
        /// </summary>
        public void BeginJoinWithCode(string code)
        {
            _pendingMode = "JOIN";
            _prefillCode = code;
            UI.Show("net-name");
        }

        string _prefillCode;

        /// <summary>Re-render the dreidel picker on this screen - used when an unlock lands.</summary>
        public void RefreshSkinPicker()
        {
            if (_nameSkinPicker != null && UI.Current == "net-name") OnNameScreenShown();
        }

        public void BeginHost() { _pendingMode = "HOST"; UI.Show("net-name"); }
        public void BeginJoin() { _pendingMode = "JOIN"; UI.Show("net-name"); }
        public void BeginQuickMatch() { _pendingMode = "QUICK"; UI.Show("net-name"); }

        /// <summary>
        /// The one place a mode becomes a transport. Everything downstream - lobby, reconnect,
        /// takeover - reads INetTransport, so this is the only line that has to know.
        /// </summary>
        INetTransport MakeTransport()
        {
            return _online ? (INetTransport)new RelayTransport() : new LanTransport();
        }

        void RenderModePicker()
        {
            if (_modeRow == null) return;
            UIKit.Clear(_modeRow);
            UIKit.Chip(_modeRow, "Online", _online, () =>
            {
                Sfx.Play("tick"); SetMode(true);
            }, 160f, 46f, 15);
            UIKit.Chip(_modeRow, "Same Wi-Fi", !_online, () =>
            {
                Sfx.Play("tick"); SetMode(false);
            }, 160f, 46f, 15);

            if (_modeNote != null)
                _modeNote.text = _online
                    ? "Play with anyone, <color=#f2c14e><b>anywhere</b></color>. "
                      + "Works on mobile data."
                    : "Everyone needs to be on the <color=#f2c14e><b>same Wi-Fi</b></color>. "
                      + "No internet needed.";
        }

        void SetMode(bool online)
        {
            _online = online;
            Store.Set("drdl-net-online", online ? "1" : "0");
            RenderModePicker();
        }

        public void OnNameScreenShown()
        {
            var saved = Store.Get("drdl-net-online");
            if (!string.IsNullOrEmpty(saved)) _online = saved == "1";

            if (_nameTag != null)
                _nameTag.text = string.IsNullOrEmpty(_prefillCode)
                    ? "Pick your table name"
                    : "Joining table <color=#f2c14e><b>" + _prefillCode + "</b></color> - pick your name";

            // Quick Match asks the local network who is open, and only a Wi-Fi table can
            // answer that, so it has no choice to offer.
            bool pickable = _pendingMode != "QUICK";
            if (_modeRow != null) _modeRow.gameObject.SetActive(pickable);
            if (_modeNote != null && _modeNote.transform.parent != null)
                _modeNote.transform.parent.gameObject.SetActive(pickable);
            if (pickable) RenderModePicker();

            Pickers.RenderSkin(_nameSkinPicker, GC.MySkinChoice, id =>
            {
                GC.MySkinChoice = id;
                Store.Set("drdl-skin", id);
                GC.View.SetSkin(id);
                if (Net.Active && !Net.IsHost) Net.SendSkin(id);
                OnNameScreenShown();
            });
        }

        /// <summary>The code screen inherits the mode chosen a screen earlier, so it says so.</summary>
        public void OnCodeScreenShown()
        {
            if (_codeTag != null)
                _codeTag.text = _online ? "Ask the host for their room code"
                                        : "Ask the host for their 4 letters";
            if (_codeHint != null)
                _codeHint.text = _online
                    ? "You can be anywhere - the host doesn't need to be on your Wi-Fi."
                    : "On a network that blocks discovery, type the host's IP address instead of the code.";
            if (_joinStatus != null) _joinStatus.text = "";
        }

        string MyName()
        {
            var n = _nameInput != null ? _nameInput.text : "";
            return Player.CleanName(n, 10);
        }

        void ConfirmName()
        {
            var n = MyName();

            // The same secret the web build accepted here. It is debug-gated inside, so on a
            // release build this is just a name.
            if (UI.CheckTestUnlock(n)) { _nameInput.text = ""; OnNameScreenShown(); return; }

            Store.Set("drdl-name", n);
            Sfx.Play("tick");

            if (!string.IsNullOrEmpty(_prefillCode))
            {
                var code = _prefillCode;
                _prefillCode = null;
                Net.JoinGame(MakeTransport(), code, n);
                return;
            }

            if (_pendingMode == "HOST") Net.HostGame(MakeTransport(), n);
            else if (_pendingMode == "QUICK")
            {
                UI.Show("net-quick");
                SetQuickStatus("Searching for open tables...");
                Net.QuickMatch(n);
            }
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
                if (IsOnlineTable())
                {
                    _lobbyHint.text = "Anyone can join from anywhere with this code";
                }
                else
                {
                    // A host on a network that blocks broadcast can still be reached by
                    // address, so the address is shown rather than left for someone to go
                    // hunting for.
                    var addrs = isHost ? LanTransport.LocalAddresses() : new List<string>();
                    _lobbyHint.text = addrs.Count > 0
                        ? "Same Wi-Fi as you. If the code won't find it, they can type " + addrs[0]
                        : "Everyone needs to be on the same Wi-Fi";
                }
            }
            RefreshLobby();
        }

        /// <summary>
        /// The code, plus the address, because on a network that blocks discovery the address
        /// is the thing that actually gets someone in - and it is far easier to tap a message
        /// than to read an IP off someone else's screen.
        /// </summary>
        void ShareInvite()
        {
            Sfx.Play("tick");
            var code = Net.RoomCodeText ?? "";
            if (string.IsNullOrEmpty(code)) { UI.Toast("No room code yet - one moment", true); return; }
            var body = "Join my Dreidel Royale table - the code is " + code + "."
                     + "\n\nOpen Dreidel Royale, tap Join, and enter " + code + ".";

            if (IsOnlineTable())
            {
                body += " You can be anywhere.";
            }
            else
            {
                body += " We need to be on the same Wi-Fi.";
                var addrs = LanTransport.LocalAddresses();
                if (addrs.Count > 0)
                    body += "\n\nIf the code doesn't find it, enter this instead: " + addrs[0];
            }

            if (!NativeShare.Share("Dreidel Royale", body))
                UI.Toast("Invite copied to the clipboard");
        }

        bool IsOnlineTable()
        {
            return Net != null && Net.Transport != null && Net.Transport.IsOnline;
        }

        public void SetLobbyStatus(string text, bool good)
        {
            if (_lobbyStatus == null) return;
            _lobbyStatus.text = "- " + text;
            _lobbyStatus.color = good ? Theme.Ok : Theme.Sub;
        }

        public void JoinFailed(string why)
        {
            // Show first, then write the reason: the screen's own on-shown pass clears the
            // status line, so setting it beforehand would wipe the one thing worth reading.
            // A host that never opened is sent back to the name screen rather than to the
            // join screen, which would be asking them for a code they were never given.
            if (Net != null && Net.IsHost)
            {
                Net.LeaveEverything();
                GC.IsLocalGame = true;
                _pendingMode = "HOST";
                UI.Show("net-name");
                return;                      // the caller toasts the reason
            }

            if (UI.Current == "net-lobby") UI.Show("net-code");
            if (_joinStatus != null) _joinStatus.text = why;
        }

        /// <summary>
        /// Game style and stakes belong to the host, so a guest's tap is answered with the
        /// reason rather than silently ignored - the chips look tappable either way.
        /// </summary>
        void RenderLobbyRules()
        {
            if (_lobbyRulesPicker == null) return;
            var sel = string.IsNullOrEmpty(GC.G.Rules) ? "rising" : GC.G.Rules;

            UIKit.Clear(_lobbyRulesPicker);
            foreach (var r in Rules.Defs)
            {
                var cr = r;
                UIKit.Chip(_lobbyRulesPicker, r.Label, sel == r.Id, () =>
                {
                    if (!Net.IsHost) { UI.Toast("Only the host sets the game style", true); return; }
                    GC.G.Rules = cr.Id;
                    GC.RulesMode = cr.Id;
                    Store.Set("drdl-rules", cr.Id);
                    Sfx.Play("tick"); Sfx.Buzz(10);
                    Net.Broadcast();
                    RefreshLobby();
                }, 100f, 40f, 13);
            }

            UIKit.Clear(_lobbyAntePicker);
            int ante = Mathf.Max(1, GC.G.Ante);
            for (int n = 1; n <= 3; n++)
            {
                int cn = n;
                UIKit.Chip(_lobbyAntePicker, n.ToString(), ante == n, () =>
                {
                    if (!Net.IsHost) { UI.Toast("Only the host sets the ante", true); return; }
                    GC.G.Ante = cn; GC.G.BaseAnte = cn; GC.AnteAmount = cn;
                    Store.Set("drdl-ante", cn.ToString());
                    Sfx.Play("tick"); Sfx.Buzz(10);
                    Net.Broadcast();
                    RefreshLobby();
                }, 48f, 40f, 15);
            }

            if (_lobbyAnteLabel != null)
                _lobbyAnteLabel.text = (sel == "classic"
                    ? "Starting ante - fixed all game"
                    : "Starting ante - rises every " + Rules.RiseEveryFor(sel) + " rounds").ToUpper();
        }

        public void RefreshLobby()
        {
            RenderLobbyRules();
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
