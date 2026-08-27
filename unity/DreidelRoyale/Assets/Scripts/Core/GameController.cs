using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DreidelRoyale.Audio;
using DreidelRoyale.UI;
using DreidelRoyale.Visual;
using Random = UnityEngine.Random;

namespace DreidelRoyale.Core
{
    /// <summary>
    /// The game itself: the turn loop, the spin and its consequences, the bots, and the
    /// bookkeeping that turns a finished game into unlocks. The web build runs this off
    /// chained timeouts; here it is one coroutine per spin, with the same beats.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public static GameController I;

        public GameState G = new GameState();
        public DreidelView View;
        public Hud Hud;
        public UIManager UI;
        public MusicEngine Music;

        public bool IsLocalGame = true;
        public bool IsSpinning, IsCharging;
        public bool CustomMode;
        public string[] CustomLabels = { "", "", "", "" };

        public string MySkinChoice = Store.Get("drdl-skin") ?? "wood";
        public string HostEnvChoice = Store.Get("drdl-env") ?? "midnight";
        public string RulesMode = Store.Get("drdl-rules") ?? "rising";
        public string CpuDiff = Store.Get("drdl-cpudiff") ?? "medium";
        public int CpuCount = 1;
        public int AnteAmount = Mathf.Clamp(Store.Int("drdl-ante", 1), 1, 3);

        int _startingPlayers;
        bool _showdownShown;
        int _lastTurnIndex = -1;
        Coroutine _spinRoutine, _dangerRoutine;
        Action<float> _pendingImpact;

        public string AppliedEnv { get; private set; }

        /// <summary>Raised after a table is dressed, so the AR layer can honour its wishes.</summary>
        public Action<EnvDef> OnEnvApplied;

        void Awake() { I = this; }

        // ---------------------------------------------------------------
        public void ApplyEnv(string id)
        {
            AppliedEnv = id;
            var env = EnvDefs.Get(id);
            View.SetEnv(env);
            if (UI != null) UI.ApplyEnvBackdrop(env);
            if (OnEnvApplied != null) OnEnvApplied(env);
        }

        // ---------------------------------------------------------------
        //  starting a game
        // ---------------------------------------------------------------
        public void StartCpuGame(string playerName)
        {
            IsLocalGame = true;
            CustomMode = false;
            View.SetCustomFaces(false, null);

            G.Players = new List<Player>
            {
                new Player("HUMAN", string.IsNullOrEmpty(playerName) ? "You" : Trim(playerName, 10), Consts.StartCoins)
            };
            var pool = CpuBrain.Names.OrderBy(_ => Random.value).ToList();
            for (int i = 0; i < CpuCount; i++)
                G.Players.Add(new Player("CPU" + i, pool[i], Consts.StartCoins, true));

            G.Env = HostEnvChoice; ApplyEnv(HostEnvChoice);
            G.Ante = AnteAmount; G.BaseAnte = AnteAmount; G.Rules = RulesMode;
            StartCoroutine(Countdown(() =>
            {
                BeginPlay();
                MaybeCpuTurn();   // in case a bot leads off; the human is always seat 0
            }));
        }

        public void StartLocalGame()
        {
            IsLocalGame = true;
            CustomMode = false;
            View.SetCustomFaces(false, null);
            G.Env = HostEnvChoice; ApplyEnv(HostEnvChoice);
            G.Ante = AnteAmount; G.BaseAnte = AnteAmount; G.Rules = RulesMode;
            StartCoroutine(Countdown(BeginPlay));
        }

        /// <summary>
        /// Decision Dreidel: the same 3D dreidel and spin, but the faces show the user's text
        /// and there is no pot, no gelt and no turns.
        /// </summary>
        public void StartCustomGame(string[] labels)
        {
            IsLocalGame = true;
            CustomMode = true;
            CustomLabels = labels;
            View.SetCustomFaces(true, labels);
            G.Players = new List<Player> { new Player("HUMAN", "You", 0) };
            G.Status = GameStatus.Playing;
            G.Pot = 0; G.Round = 1; G.TurnIndex = 0;
            G.Stats = new GameStats();
            View.SetPotCoins(0);
            Music.SetIntensity(1);
            UI.ShowGame();
            Hud.Refresh();
        }

        void BeginPlay()
        {
            G.Status = GameStatus.Playing;
            G.Pot = 0;
            G.Round = 1;
            G.TurnIndex = 0;
            G.Ante = Mathf.Max(1, G.BaseAnte);   // stakes reset with the rounds
            foreach (var p in G.Players)
            {
                p.Coins = Rules.StartCoinsFor(G.Rules);
                p.Eliminated = false;
            }
            Hud.ResetMotion();
            G.Stats = new GameStats();
            _startingPlayers = G.Players.Count;
            _showdownShown = false;
            _lastTurnIndex = -1;
            Music.SetIntensity(1);
            AnteUp(true);
            UI.ShowGame();
            Hud.Refresh();
        }

        public IEnumerator Countdown(Action done)
        {
            UI.ShowCountdown("3"); Sfx.Play("tick");
            yield return new WaitForSeconds(0.9f);
            UI.ShowCountdown("2"); Sfx.Play("tick");
            yield return new WaitForSeconds(0.9f);
            UI.ShowCountdown("1"); Sfx.Play("tick");
            yield return new WaitForSeconds(0.9f);
            UI.ShowCountdown("SPIN!"); Sfx.Play("go"); Sfx.Buzz(40);
            yield return new WaitForSeconds(0.9f);
            UI.HideCountdown();
            if (done != null) done();
        }

        // ---------------------------------------------------------------
        //  rules
        // ---------------------------------------------------------------
        public void AnteUp(bool silent = false)
        {
            int a = Mathf.Max(1, G.Ante);
            int active = 0;
            var busted = new List<Player>();
            foreach (var p in G.Players)
            {
                if (p.Eliminated) continue;
                // pay the ante, or go short-stacked all-in; only 0 gelt knocks you out
                if (p.Coins > 0) { int pay = Mathf.Min(p.Coins, a); p.Coins -= pay; G.Pot += pay; active++; }
                else { p.Eliminated = true; busted.Add(p); }
            }
            if (!silent)
            {
                for (int i = 0; i < busted.Count; i++)
                {
                    var bp = busted[i];
                    UI.Toast(bp.Name + " is out of gelt!", true);
                    Sfx.Play("elim");
                    if (bp.Cpu && IsLocalGame) StartCoroutine(QuipAfter(bp, "ELIM", 0.5f + i * 0.7f));
                }
            }
            CheckWinCondition(active);
        }

        void CheckWinCondition(int activeCount)
        {
            if (activeCount > 1) return;
            var winner = G.Players.FirstOrDefault(p => !p.Eliminated);
            G.Status = GameStatus.GameOver;
            ShowWinner(winner != null ? winner.Name : "Nobody");
        }

        void MaybeRaiseAnte()
        {
            if (G.Status != GameStatus.Playing || G.Rules == "classic") return;
            int target = Rules.AnteTargetFor(G);
            if (target <= Mathf.Max(1, G.Ante)) return;
            G.Ante = target;
            Music.SetIntensity(2);
            StartCoroutine(AnnounceStakes(target));
        }

        IEnumerator AnnounceStakes(int target)
        {
            // a plain wait, not one of the spin's timers, so the next spin's flush can't eat it
            yield return new WaitForSeconds(1.4f);
            UI.Toast("Stakes rise — ante is now " + target);
            Sfx.Play("go");
            Sfx.Buzz(30, 40, 60);
        }

        void TriggerShowdown()
        {
            if (_showdownShown) return;
            _showdownShown = true;
            UI.ShowShowdownBanner();
            Music.SetIntensity(2);
            View.SetDrama(true);
            Sfx.Play("elim");
            Sfx.Buzz(50, 60, 50, 60, 120);
        }

        // ---------------------------------------------------------------
        //  the spin
        // ---------------------------------------------------------------
        public bool MyTurn()
        {
            var p = G.Current;
            if (p == null) return false;
            if (p.Cpu) return false;          // CPU turns are hands-free; humans can't spin for them
            return IsLocalGame;
        }

        public void UserTriggerSpin(float power)
        {
            if (IsSpinning) return;
            ExecutePhysicsSpin(power);
        }

        void ExecutePhysicsSpin(float power)
        {
            if (IsSpinning) return;
            power = Mathf.Clamp(power, 0.15f, 1f);
            float baseRot = 1080f + power * 2160f;
            float variance = Random.value * 720f;
            float totalDelta = baseRot + variance;
            float duration = 2.2f + power * 3.2f;
            float wobble = Random.value * 14f - 7f;

            float final = View.GetRotDeg() - totalDelta;          // authoritative landing rotation
            var side = Rules.ResolveFace(final);
            final = side.Angle + 360f * Mathf.Round((final - side.Angle) / 360f);   // land square on the face

            if (_spinRoutine != null) StopCoroutine(_spinRoutine);
            _spinRoutine = StartCoroutine(PerformSpin(totalDelta, final, wobble, duration, power));
        }

        /// <summary>
        /// Launch, whirl, chant, crane, land. The topple starts at `duration`, but the SLAM —
        /// body meeting surface — lands later, and with the fake-out its timing varies. So the
        /// physical consequences are armed here and fired by the view's impact callback at the
        /// true contact frame.
        /// </summary>
        IEnumerator PerformSpin(float delta, float final, float wobble, float duration, float power)
        {
            IsSpinning = true;
            _pendingImpact = null;
            Hud.HideResultCard();
            Hud.SetSpinButton(false, "SPINNING");

            View.SetCam("default");
            View.StartSpin(final, delta, duration, power, wobble);

            Sfx.Play("launch", power);
            Sfx.Play("whirl", power);
            Sfx.Buzz(20, 30, Mathf.RoundToInt(20 + power * 50));

            // ---- CHANTS ----
            float step = duration / (Consts.Chants.Length + 1);
            for (int i = 0; i < Consts.Chants.Length; i++)
                StartCoroutine(ChantAt(step * (i + 0.6f), Consts.Chants[i], power));

            // ---- CRANE SHOT ----
            StartCoroutine(CraneAt(duration * 0.68f));

            yield return new WaitForSeconds(duration);

            // ---- LANDING ----
            UI.SetDim(0f);

            // Celebration splash — special dreidels erupt a colour-matched spark fountain when
            // they land, so unlocking or owning them feels rewarding. Purely cosmetic.
            Color burstCol;
            if (SkinLibrary.BurstColor.TryGetValue(View.CurrentSkin ?? "", out burstCol))
                StartCoroutine(SkinBurstAt(0.03f, burstCol));

            var side = Rules.ResolveFace(final);

            if (CustomMode)
            {
                // Decision Dreidel: show the user's label for this face, no pot, no gelt.
                Hud.ShowResult("", CustomLabelFor(side), "", "");
                _pendingImpact = _ =>
                {
                    Sfx.Play("coin"); Sfx.Buzz(30, 40, 30);
                    UI.Fx.Confetti(Screen.width / 2f, Screen.height * 0.45f, 50, 12);
                };
                Hud.RevealResultCard();
                Hud.SetSpinButton(true, "SPIN AGAIN");
                IsSpinning = false;
                yield break;
            }

            Hud.ShowResult(side.Char, side.Phon, Consts.FlavorText(side, AppliedEnv), "");

            // Each face gets its physical verb, synced to the slam. Amounts mirror the apply
            // logic below — they are recomputed there for the actual payout; this is the visual.
            if (side.Name == "GIMEL")
            {
                _pendingImpact = pw => { if (View.Gelt.Scatter(pw)) Sfx.Play("coin"); };
            }
            else if (side.Name == "HEI")
            {
                int take = Mathf.CeilToInt(G.Pot / 2f);          // the pot visibly cleaves
                _pendingImpact = pw => { if (View.Gelt.Cleave(take, pw)) Sfx.Play("coin"); };
            }
            else if (side.Name == "NUN")
            {
                // the anti-juice: a soft flop, a sad puff of dust, and the flames sigh
                _pendingImpact = _ => { Sfx.Play("flop"); View.FlameSigh(); };
            }
            else
            {
                // SHIN/PEI: the payment arrives as objects — coins arc in a beat after the slam
                var cur = G.Current;
                int a = Rules.ShinCost(G);
                int pay = Mathf.Min(cur != null ? cur.Coins : 1, a);
                if (pay > 0) _pendingImpact = _ => StartCoroutine(PayInAfter(0.65f, pay));
            }

            // The card must not spoil the fake-out: with the near-miss the slam lands late, and
            // announcing GIMEL while the top is still clawing back upright kills the drama. So
            // the reveal rides the impact plus a beat, with a timeout backstop. Nun gets a
            // longer, deliberately awkward pause: the tumbleweed beat.
            bool revealed = false;
            Action reveal = () =>
            {
                if (revealed) return;
                revealed = true;
                Hud.RevealResultCard();
                switch (side.Name)
                {
                    case "GIMEL":
                        Sfx.Play("gimel");
                        UI.Fx.Confetti();
                        UI.Fx.FountainGelt(Hud.PotBox, G.Pot);
                        View.Burst();
                        Sfx.Buzz(40, 60, 40, 60, 120);
                        break;
                    case "HEI": Sfx.Play("stinger-hei"); Sfx.Buzz(30, 50, 30); break;
                    case "NUN": Sfx.Play("stinger-nun"); Sfx.Buzz(20); break;
                    default: Sfx.Play("stinger-shin"); Sfx.Buzz(20, 20, 20); break;
                }
            };

            var prev = _pendingImpact;
            _pendingImpact = pw =>
            {
                if (prev != null) prev(pw);
                StartCoroutine(Delayed(side.Name == "NUN" ? 0.75f : 0.18f, reveal));
            };
            StartCoroutine(Delayed(2.2f, reveal));   // backstop: never leave the result unannounced

            // ---- APPLY ----
            yield return new WaitForSeconds(2.1f);
            ApplyOutcome(side);

            yield return new WaitForSeconds(0.7f);
            IsSpinning = false;
            Hud.Refresh();
            var nx = G.Current;
            if (G.Status == GameStatus.Playing && nx != null)
            {
                if (nx.Cpu) MaybeCpuTurn();
                else if (!G.Players.Any(p => p.Cpu)) UI.Toast("Pass to " + nx.Name);
            }
        }

        void ApplyOutcome(Side side)
        {
            var p = G.Current;
            if (p == null) return;

            string outcome;
            int sweptN = 0, paidN = -1;
            var st = G.Stats;
            st.Spins++;
            st.History.Add(side.Name);      // the spin log for the share card

            if (side.Name == "GIMEL")
            {
                int won = G.Pot;
                p.Coins += won; G.Pot = 0;
                st.Gimels[p.Id] = st.GimelsFor(p.Id) + 1;
                if (won > st.BiggestSweep) { st.BiggestSweep = won; st.SweepBy = p.Name; }
                sweptN = won;
                outcome = string.Format("{0} sweeps {1} gelt!", p.Name, won);
                UI.Fx.FlyGelt(Hud.PotBox, Hud.RowFor(G.TurnIndex), won);
                Hud.PopDelta(G.TurnIndex, "+" + won, true);
            }
            else if (side.Name == "HEI")
            {
                int h = Mathf.CeilToInt(G.Pot / 2f);
                p.Coins += h; G.Pot -= h;
                outcome = string.Format("{0} takes {1} gelt", p.Name, h);
                UI.Fx.FlyGelt(Hud.PotBox, Hud.RowFor(G.TurnIndex), h);
                Hud.PopDelta(G.TurnIndex, "+" + h, true);
            }
            else if (side.Name == Consts.Fourth().Name)
            {
                // classic rules: the authentic "shtel" — put one in. Otherwise pay the ante.
                int a = Rules.ShinCost(G);
                int pay = Mathf.Min(p.Coins, a);
                paidN = pay;
                if (pay > 0)
                {
                    p.Coins -= pay; G.Pot += pay;
                    outcome = string.Format("{0} pays {1} to the pot", p.Name, pay);
                    UI.Fx.FlyGelt(Hud.RowFor(G.TurnIndex), Hud.PotBox, pay);
                    Hud.PopDelta(G.TurnIndex, "−" + pay, false);
                }
                else outcome = p.Name + " has nothing left to pay";
            }
            else outcome = p.Name + " gets nothing";

            Hud.SetOutcome(outcome);
            TableTalk(p, side, sweptN, paidN);

            // whenever the pot is emptied, everyone antes up again to refill it — regardless of
            // which face emptied it
            if (G.Pot == 0) AnteUp();

            AdvanceTurn();
        }

        void AdvanceTurn()
        {
            int nextIdx = (G.TurnIndex + 1) % G.Players.Count, safety = 0;
            // skip dropped seats too — they are held for reconnect, but the table doesn't wait
            while ((G.Players[nextIdx].Eliminated || G.Players[nextIdx].Disconnected) && safety < 16)
            {
                nextIdx = (nextIdx + 1) % G.Players.Count; safety++;
            }
            if (nextIdx < G.TurnIndex) { G.Round++; MaybeRaiseAnte(); }
            G.TurnIndex = nextIdx;

            int alive = G.AliveCount;
            if (alive == 2 && _startingPlayers > 2 && G.Status == GameStatus.Playing) TriggerShowdown();
        }

        // ---- small scheduled beats ----
        IEnumerator ChantAt(float t, string word, float power)
        {
            yield return new WaitForSeconds(t);
            UI.Chant(word, power);
            UI.SetDim(0.22f + power * 0.2f);
            Sfx.Play("chant", power);
            Sfx.Buzz(Mathf.RoundToInt(10 + power * 15));
            yield return new WaitForSeconds(0.22f);
            UI.SetDim(0f);
        }

        IEnumerator CraneAt(float t) { yield return new WaitForSeconds(t); View.SetCam("crane"); }
        IEnumerator SkinBurstAt(float t, Color c) { yield return new WaitForSeconds(t); View.SkinBurst(c, 38, 0.8f); }
        IEnumerator PayInAfter(float t, int pay) { yield return new WaitForSeconds(t); if (View.Gelt.PayIn(pay)) Sfx.Play("coin"); }
        IEnumerator Delayed(float t, Action a) { yield return new WaitForSeconds(t); if (a != null) a(); }

        public void FireImpact(float power)
        {
            var pi = _pendingImpact;
            _pendingImpact = null;
            if (pi != null) pi(power);
        }

        public string CustomLabelFor(Side side)
        {
            int i = Array.IndexOf(Consts.Sides, side);
            if (i < 0) i = 0;
            var l = (CustomLabels != null && i < CustomLabels.Length) ? CustomLabels[i] : null;
            return string.IsNullOrEmpty(l) ? side.Phon : l;
        }

        // ---------------------------------------------------------------
        //  bots
        // ---------------------------------------------------------------
        public bool IsCpuTurn()
        {
            var p = G.Current;
            return p != null && p.Cpu && !p.Eliminated && G.Status == GameStatus.Playing;
        }

        public void MaybeCpuTurn()
        {
            if (!IsCpuTurn()) return;
            StartCoroutine(CpuTurn());
        }

        IEnumerator CpuTurn()
        {
            var d = CpuBrain.Diff(CpuDiff);
            Hud.SetCpuThinking(true);       // pulsing avatar and a "thinking" cue
            yield return new WaitForSeconds(d.ThinkLo + Random.value * (d.ThinkHi - d.ThinkLo));
            if (!IsCpuTurn() || IsSpinning || IsCharging) { Hud.SetCpuThinking(false); yield break; }
            Hud.SetCpuThinking(false);
            yield return StartCoroutine(CpuSpin());
        }

        /// <summary>
        /// Spin strength scales with difficulty; the landing itself stays pure chance. The bot
        /// winds the same ring a human does, then goes through the identical resolution path.
        /// </summary>
        IEnumerator CpuSpin()
        {
            var d = CpuBrain.Diff(CpuDiff);
            float power = d.Lo + Random.value * (d.Hi - d.Lo);
            IsCharging = false;
            View.SetCam("charge");
            Hud.SetSpinButton(false, "SPINNING");
            Sfx.StartRumble();
            View.ChargeStart();

            float t0 = Time.time;
            while (true)
            {
                float f = Mathf.Min((Time.time - t0) / 0.65f, 1f);
                Hud.SetPowerRing(power * f);
                View.ChargeSet(power * f);
                Sfx.SetRumble(power * f);
                if (f >= 1f) break;
                yield return null;
            }

            Sfx.StopRumble();
            Hud.SetPowerRing(0f);
            View.ChargeEnd();
            View.SetCam("default");
            Hud.SetSpinButton(false, "…");
            UserTriggerSpin(power);         // exact same resolution path as a human release
        }

        // ---- table talk ----
        void TableTalk(Player p, Side side, int sweptN, int paidN)
        {
            if (!IsLocalGame || !G.Players.Any(q => q.Cpu)) return;
            var rivals = G.Players.Where(q => q.Cpu && !q.Eliminated).ToList();
            var rival = rivals.Count > 0 ? rivals[Random.Range(0, rivals.Count)] : null;

            if (p.Cpu && Random.value < 0.65f)
            {
                string key = side.Name == "GIMEL" ? (sweptN >= 8 ? "BIG" : "GIMEL") : side.Name;
                if ((side.Name == "SHIN" || side.Name == "PEI") && paidN == 0) key = "BROKE";
                StartCoroutine(QuipAfter(p, key, 0.3f));     // a human beat, not a pause
            }
            else if (!p.Cpu && side.Name == "GIMEL" && Random.value < 0.55f && rival != null)
                StartCoroutine(QuipAfter(rival, "JAB", 0.45f));
            else if (!p.Cpu && (side.Name == "SHIN" || side.Name == "PEI") && Random.value < 0.3f && rival != null)
                StartCoroutine(QuipAfter(rival, "JAB_PAY", 0.4f));
        }

        IEnumerator QuipAfter(Player bot, string key, float delay)
        {
            yield return new WaitForSeconds(delay);
            var line = CpuBrain.DrawLine(key);
            if (line == null) yield break;
            int idx = G.Players.IndexOf(bot);
            if (idx < 0) yield break;
            Hud.ShowQuip(idx, line);
        }

        // ---------------------------------------------------------------
        //  the end
        // ---------------------------------------------------------------
        public void ShowWinner(string name)
        {
            ClearCpuSave();
            var st = G.Stats;
            var w = G.Players.FirstOrDefault(p => !p.Eliminated);
            int wGimels = w != null ? st.GimelsFor(w.Id) : 0;

            var human = G.Players.FirstOrDefault(p => p.Id == "HUMAN");
            bool isCpuGame = G.Players.Any(p => p.Cpu);
            bool humanWon = w != null && w.Id == "HUMAN";
            bool humanLost = isCpuGame && human != null && !humanWon;

            // persist lifetime stats
            var S = Stats.Load();
            var pre = S.Clone();                 // snapshot for the unlock diff
            S.games++;
            S.spins += st.Spins;
            S.gimels += st.TotalGimels();
            if (isCpuGame)
            {
                if (humanWon)
                {
                    S.wins++; S.streak++;
                    if (S.streak > S.bestStreak) S.bestStreak = S.streak;
                }
                else { S.losses++; S.streak = 0; }
            }
            if (st.BiggestSweep > S.bestSweep) S.bestSweep = st.BiggestSweep;
            Stats.Save(S);

            // any dreidels OR tables newly earned this game?
            var newly = new List<KeyValuePair<string, string>>();
            foreach (var d in Unlocks.Skins)
                if (d.Unlock != null && !d.Unlock.Test(pre) && d.Unlock.Test(S))
                    newly.Add(new KeyValuePair<string, string>(d.Name, "dreidel"));
            foreach (var e in Unlocks.EnvUnlocks)
                if (e.Unlock != null && !e.Unlock.Test(pre) && e.Unlock.Test(S))
                    newly.Add(new KeyValuePair<string, string>(e.Name, "table"));

            float delay = 1.6f;
            foreach (var u in newly)
            {
                StartCoroutine(AnnounceUnlock(delay, u.Value, u.Key));
                delay += 1.1f;
            }

            UI.ShowWinner(w != null ? w.Name : name, humanLost, G.Round, st.Spins, wGimels, st.BiggestSweep, S);

            View.SetDrama(false);
            Music.SetIntensity(0);

            if (humanLost)
            {
                // defeat: subdued, no confetti storm
                Sfx.Play("lose");
                Sfx.Buzz(120, 60, 120);
            }
            else
            {
                Sfx.Play("win");
                Sfx.Buzz(60, 80, 60, 80, 200);
                StartCoroutine(VictoryLap(w));
                UI.Fx.Confetti(Screen.width / 2f, Screen.height * 0.65f, 140, 22);
                StartCoroutine(ConfettiRain());
            }

            if (_dangerRoutine != null) { StopCoroutine(_dangerRoutine); _dangerRoutine = null; }
        }

        /// <summary>
        /// The winner's dreidel stands up and takes a celebratory spin. It keeps whatever
        /// dreidel they chose; gold only fills in for the default wood, so a custom skin is
        /// never replaced.
        /// </summary>
        IEnumerator VictoryLap(Player w)
        {
            string wSkin = w != null && w.Id == "HUMAN" ? MySkinChoice : (w != null ? w.Skin : "");
            bool valid = Unlocks.ValidSkin(wSkin);
            View.SetSkin(valid && wSkin != "wood" ? wSkin : "gold");
            View.StartRecover(0.5f);
            yield return new WaitForSeconds(0.65f);
            View.Burst();
            View.StartSpin(View.GetRotDeg() - 1440f, 1440f, 3.2f, 0.85f, 4f);
        }

        IEnumerator ConfettiRain()
        {
            for (int i = 0; i <= 10; i++)
            {
                UI.Fx.Confetti(Random.value * Screen.width, Screen.height + 20f, 30, 8);
                yield return new WaitForSeconds(0.6f);
            }
        }

        IEnumerator AnnounceUnlock(float delay, string kind, string name)
        {
            yield return new WaitForSeconds(delay);
            UI.Toast(string.Format("New {0} earned — {1}!", kind, name));
            Sfx.Play("coin");
            Sfx.Buzz(40, 60, 40);
            UI.Fx.Confetti(Screen.width / 2f, Screen.height * 0.5f, 70, 14);
        }

        public void Rematch()
        {
            UI.HideWinner();
            View.SetDrama(false);
            BeginPlay();
            MaybeCpuTurn();
        }

        // ---------------------------------------------------------------
        //  single-player save/resume: state persists at every turn boundary
        // ---------------------------------------------------------------
        [Serializable]
        class SaveBlob { public List<Player> players; public int pot, turnIndex, round, ante, baseAnte; public string rules, env, diff; }

        public bool IsCpuGameNow { get { return IsLocalGame && G.Players.Any(p => p.Cpu); } }

        public void SaveCpuGame()
        {
            if (!IsCpuGameNow || G.Status != GameStatus.Playing) return;
            try
            {
                var blob = new SaveBlob
                {
                    players = G.Players, pot = G.Pot, turnIndex = G.TurnIndex, round = G.Round,
                    ante = G.Ante, baseAnte = G.BaseAnte, rules = G.Rules, env = G.Env, diff = CpuDiff
                };
                Store.Set("drdl-save", JsonUtility.ToJson(new Wrapper { blob = blob }));
            }
            catch { }
        }

        [Serializable] class Wrapper { public SaveBlob blob; }

        public void ClearCpuSave() { Store.Set("drdl-save", ""); }

        public bool HasCpuSave() { return SavedRound() > 0; }

        /// <summary>The round a saved game would resume at, or 0 when there is nothing saved.</summary>
        public int SavedRound()
        {
            try
            {
                var raw = Store.Get("drdl-save");
                if (string.IsNullOrEmpty(raw)) return 0;
                var w = JsonUtility.FromJson<Wrapper>(raw);
                if (w == null || w.blob == null || w.blob.players == null || w.blob.players.Count < 2) return 0;
                return Mathf.Max(1, w.blob.round);
            }
            catch { return 0; }
        }

        public bool ResumeCpuGame()
        {
            try
            {
                var raw = Store.Get("drdl-save");
                if (string.IsNullOrEmpty(raw)) return false;
                var w = JsonUtility.FromJson<Wrapper>(raw);
                if (w == null || w.blob == null || w.blob.players == null || w.blob.players.Count < 2) return false;
                var b = w.blob;
                IsLocalGame = true; CustomMode = false;
                View.SetCustomFaces(false, null);
                G.Players = b.players; G.Pot = b.pot; G.TurnIndex = b.turnIndex; G.Round = b.round;
                G.Ante = b.ante; G.BaseAnte = b.baseAnte; G.Rules = b.rules; G.Env = b.env;
                CpuDiff = b.diff ?? CpuDiff;
                G.Status = GameStatus.Playing;
                G.Stats = new GameStats();
                _startingPlayers = G.Players.Count;
                _showdownShown = false; _lastTurnIndex = -1;
                ApplyEnv(G.Env);
                Music.SetIntensity(1);
                UI.ShowGame();
                Hud.ResetMotion();
                Hud.Refresh();
                MaybeCpuTurn();
                return true;
            }
            catch { return false; }
        }

        // ---------------------------------------------------------------
        public int LastTurnIndex { get { return _lastTurnIndex; } set { _lastTurnIndex = value; } }

        public void StartDangerBeat()
        {
            if (_dangerRoutine == null) _dangerRoutine = StartCoroutine(DangerBeat());
        }

        public void StopDangerBeat()
        {
            if (_dangerRoutine != null) { StopCoroutine(_dangerRoutine); _dangerRoutine = null; }
        }

        IEnumerator DangerBeat()
        {
            while (true) { Sfx.Play("heartbeat"); yield return new WaitForSeconds(1.4f); }
        }

        public static string Trim(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= n ? s : s.Substring(0, n);
        }
    }
}
