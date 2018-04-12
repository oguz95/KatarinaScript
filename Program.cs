using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using LeagueSharp;
using LeagueSharp.Common;
using LeagueSharp.Common.Data;
using SharpDX;
using ItemData = LeagueSharp.Common.Data.ItemData;
using Color = System.Drawing.Color;

namespace NyanKatarina
{
    internal class Program
    {

        public const string ChampName = "Katarina";
        public const string Menun = "NyanKatarina";
        public static Menu Config;
        public static Orbwalking.Orbwalker Orbwalker;
        public static Spell Q, W, E, R;
        private static bool InUlt = false;

        private static SpellSlot Ignite;

		
        private static readonly Obj_AI_Hero Player = ObjectManager.Player;
		

		public static Vector3 wardPosition;
        public static bool jumped;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnLoad;
        }

        private static void OnLoad(EventArgs args)
        {
            if (Player.ChampionName != ChampName)
                return;

            //Ability Information - Range - Variables.
            Q = new Spell(SpellSlot.Q, 675f);
            W = new Spell(SpellSlot.W, 375f);
            E = new Spell(SpellSlot.E, 700f);
            R = new Spell(SpellSlot.R, 550f);

            Config = new Menu(Menun, Menun, true);
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));

            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));

            //Harass
            Config.AddSubMenu(new Menu("Harass", "Harass"));
            Config.SubMenu("Harass").AddItem(new MenuItem("hQ", "Use Q").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("hW", "Use W").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("hE", "Use E").SetValue(false));
            Config.SubMenu("Harass").AddItem(new MenuItem("aW", "Auto W").SetValue(true));

            //Farm
			Config.AddSubMenu(new Menu("Farm", "Farm"));
            Config.SubMenu("Farm").AddItem(new MenuItem("fq", "Use Q").SetValue(true));
            Config.SubMenu("Farm").AddItem(new MenuItem("fw", "Use W").SetValue(true));

            //Laneclear
            Config.AddSubMenu(new Menu("Clear", "Clear"));
            Config.SubMenu("Clear").AddItem(new MenuItem("lQ", "Use Q").SetValue(true));
            Config.SubMenu("Clear").AddItem(new MenuItem("lW", "Use W").SetValue(true));
            Config.SubMenu("Clear").AddItem(new MenuItem("lE", "Use E").SetValue(false));
			Config.SubMenu("Clear").AddItem(new MenuItem("jE", "Use E Jungle").SetValue(true));

            //Draw
            Config.AddSubMenu(new Menu("Draw", "Draw"));
			
			var dmg = new MenuItem("combodamage", "Damage Indicator").SetValue(true);
            var drawFill = new MenuItem("color", "Fill colour", true).SetValue(new Circle(true, Color.Orange));
            Config.SubMenu("Draw").AddItem(drawFill);
            Config.SubMenu("Draw").AddItem(dmg);

            DrawDamage.DamageToUnit = GetComboDamage;
            DrawDamage.Enabled = dmg.GetValue<bool>();
            DrawDamage.Fill = drawFill.GetValue<Circle>().Active;
            DrawDamage.FillColor = drawFill.GetValue<Circle>().Color;

            dmg.ValueChanged += delegate(object sender, OnValueChangeEventArgs eventArgs)
            {
                DrawDamage.Enabled = eventArgs.GetNewValue<bool>();
            };

            drawFill.ValueChanged += delegate(object sender, OnValueChangeEventArgs eventArgs)
            {
                DrawDamage.Fill = eventArgs.GetNewValue<Circle>().Active;
                DrawDamage.FillColor = eventArgs.GetNewValue<Circle>().Color;
            };


            //Misc
            Config.AddSubMenu(new Menu("Misc", "Misc"));
            Config.SubMenu("Misc").AddItem(new MenuItem("rcancel", "Cancel R for KS").SetValue(true));
			Config.SubMenu("Misc").AddItem(new MenuItem("autoks", "Auto KS").SetValue(true));
			Config.SubMenu("Misc").AddItem(new MenuItem("UseIgnite", "Use Ignite").SetValue(true));						
			Config.SubMenu("Misc").AddItem(new MenuItem("wardjump", "WardJump").SetValue(new KeyBind('G', KeyBindType.Press)));
            

            Config.AddToMainMenu();
            Drawing.OnDraw += OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            Obj_AI_Base.OnPlayAnimation += PlayAnimation;
        }

        private static float GetComboDamage(Obj_AI_Hero enemy)
        {
            double damage = 0d;
            
            if (Q.IsReady())
                damage += Player.GetSpellDamage(enemy, SpellSlot.Q); // + Player.GetSpellDamage(enemy, SpellSlot.Q, 1);
            
            if (W.IsReady())
                damage += Player.GetSpellDamage(enemy, SpellSlot.W);
           
            if (E.IsReady())
                damage += Player.GetSpellDamage(enemy, SpellSlot.E);
            
            if (R.IsReady() || (ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).State == SpellState.Surpressed && R.Level > 0))
                damage += Player.GetSpellDamage(enemy, SpellSlot.R) * 8;
           
            if (Ignite.IsReady())
                damage += IgniteDamage(enemy);
            
            return (float)damage;
        }


        private static void PlayAnimation(GameObject sender, GameObjectPlayAnimationEventArgs args)
        {
            if (sender.IsMe)
            {
                if (args.Animation == "Spell4")
                {
                    InUlt = true;
                }
                else if (args.Animation == "Run" || args.Animation == "Idle1" || args.Animation == "Attack2" ||
                         args.Animation == "Attack1")
                {
                    InUlt = false;
                }
            }
        }

		
        private static void combo()
        {
            var Target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
            if (Target == null || !Target.IsValidTarget())
                return;

			if (E.IsReady() && Target.IsValidTarget(E.Range))
            {
                CastE(Target);
            }
			
			if (W.IsReady() && Target.IsValidTarget(W.Range))
            {
                W.Cast();
            }
			
            if (Q.IsReady() && Target.IsValidTarget(Q.Range))
            {
                CastQ(Target);
            }

            if (R.IsReady() && !InUlt && !E.IsReady() && !Q.IsReady() && !W.IsReady() &&
                Target.IsValidTarget(R.Range))
            {
                Orbwalker.SetAttack(false);
                Orbwalker.SetMovement(false);
                
                InUlt = true;
				R.Cast();
                return;
            }

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                items();
        }

        public static void CastE(Obj_AI_Base unit)
        {			
                E.CastOnUnit(unit);			
		}

        public static void CastQ(Obj_AI_Base unit)
        {
                Q.CastOnUnit(unit);			
        }

        private static float IgniteDamage(Obj_AI_Hero target)
        {
            if (Ignite == SpellSlot.Unknown || Player.Spellbook.CanUseSpell(Ignite) != SpellState.Ready)
                return 0f;
            return (float)Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
        }

        private static void Killsteal()
        {
			if (Config.Item("autoks").GetValue<bool>())
			{
				foreach (var target in HeroManager.Enemies.Where(t => t.IsValidTarget(E.Range))) 
				{
				
                
					var Qdmg = Q.GetDamage(target);
					var Wdmg = W.GetDamage(target);
					var Edmg = E.GetDamage(target);
				
				
			 
					if (!Config.Item("rcancel").GetValue<bool>() && InUlt)
					{
					return;
						/*CancelUlt();
						InUlt = false;					
						Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);*/
					}		

					if (target != null && !target.IsMe && target.IsTargetable && !target.IsInvulnerable)
					{

						float Ignitedmg;
						if (Ignite != SpellSlot.Unknown && Ignite.IsReady() && Config.Item("UseIgnite").GetValue<bool>())
                        {
                            Ignitedmg = (float) Damage.GetSummonerSpellDamage(Player, target, Damage.SummonerSpell.Ignite);								
                        }
                        else
                        {
                            Ignitedmg = 0f;
                        }
								
						//E
						if ((target.Health < (Edmg + Ignitedmg + 20f)) && E.IsReady() && target.IsValidTarget(E.Range))                
						{
							CancelUlt();
							CastE(target);
						//	return;
						}
					
						//W
						else if ((target.Health < (Wdmg + Ignitedmg + 20f)) && W.IsReady() && target.IsValidTarget(W.Range))                
						{
							CancelUlt();
							W.Cast();
						//	return;
						}
				
						//Q
						else if ((target.Health < Qdmg + Ignitedmg + 20f) && Q.IsReady() && target.IsValidTarget(600f))                
						{
							CancelUlt();
							CastQ(target);
						//	return;
						}
					

						//EW
						else if (target.Health < (Wdmg + Edmg + Ignitedmg + 20f) && (W.IsReady() && E.IsReady()) && target.IsValidTarget(E.Range))                
						{
							CancelUlt();
							CastE(target);
							if (target != null && target.IsValidTarget(W.Range))                
							{
							W.Cast();
							}
						//	return;
						}
				
						//EQ
						else if (target.Health < (Qdmg + Edmg + Ignitedmg + 20f) && (Q.IsReady() && E.IsReady()) && target.IsValidTarget(E.Range))                
						{
							CancelUlt();
							CastE(target);
							CastQ(target);
						//	return;
						}
					
						//EWQ
						else if (target.Health < (Qdmg + Wdmg + Edmg + Ignitedmg + 20f) && (Q.IsReady() && W.IsReady() && E.IsReady()) && target.IsValidTarget(E.Range))                
						{
							CancelUlt();
							CastE(target);
							if (target != null && target.IsValidTarget(W.Range))                
							{
								W.Cast();
							}
							CastQ(target);
						//	return;
						}
					}
				}
			}	
		}
		
        private static void items()
		{
			Ignite = Player.GetSpellSlot("summonerdot");
			var target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
			if (target == null || !target.IsValidTarget() || InUlt)
                return;

            if (Player.Distance(target.Position) <= 600 && IgniteDamage(target) >= target.Health && Config.Item("UseIgnite").GetValue<bool>() && !InUlt)
                Player.Spellbook.CastSpell(Ignite, target);
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
			if (Player.IsDead) return;
			
/*			if (Player.IsDead || MenuGUI.IsChatOpen)
            {
                return;
            }
*/
			Killsteal();
			
						
			if (Config.Item("wardjump").GetValue<KeyBind>().Active)
            {	
					CancelUlt();
                    WardJumpKata();
            }
						
			if (InUlt)
            {
                Orbwalker.SetAttack(false);
                Orbwalker.SetMovement(false);
                return;
            }

			items();
			
            Orbwalker.SetAttack(true);
            Orbwalker.SetMovement(true);

            switch (Orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    combo();
                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    harass();
                    break;
                case Orbwalking.OrbwalkingMode.LastHit:
                    Farm();
                    break;
                case Orbwalking.OrbwalkingMode.LaneClear:
                    Clear();
                    break;
            }
			
			AutoW();
			
        }

        public static void WardJumpKata()
        {
            Player.IssueOrder(GameObjectOrder.MoveTo, Player.Position.Extend(Game.CursorPos, 850));
            if (E.IsReady())
            {
                wardPosition = Game.CursorPos;
                Obj_AI_Minion Wards;
                if (Game.CursorPos.Distance(Program.Player.Position) <= 700)
                {
                    Wards = ObjectManager.Get<Obj_AI_Minion>().Where(ward => ward.Distance(Game.CursorPos) < 350 && !ward.IsDead).FirstOrDefault();
                }
                else
                {
                    Vector3 cursorPos = Game.CursorPos;
                    Vector3 myPos = Player.ServerPosition;
                    Vector3 delta = cursorPos - myPos;
                    delta.Normalize();
                    wardPosition = myPos + delta * (600 - 5);
                    Wards = ObjectManager.Get<Obj_AI_Minion>().Where(ward => ward.Distance(wardPosition) < 350 && !ward.IsDead).FirstOrDefault();
                }
                if (Wards == null)
                {
                    if (!wardPosition.IsWall())
                    {
                        InventorySlot invSlot = Items.GetWardSlot();
                        Items.UseItem((int)invSlot.Id, wardPosition);
                        jumped = true;
                    }
                }
                else
                    if (E.CastOnUnit(Wards))
                    {
                        jumped = false;
                    }
            }
        }

		private static void AutoW()
        {
           var target = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Magical);
            if (target == null || !target.IsValidTarget() || InUlt || Player.IsRecalling())
                return;

            if (!InUlt && W.IsReady() && Config.Item("aW").GetValue<bool>() && target.IsValidTarget(W.Range))
                W.Cast();
        }
		
		private static void CancelUlt()
        {
			InUlt = false;
			Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
			Orbwalker.SetAttack(true);
            Orbwalker.SetMovement(true);
		}
		
        private static void Farm()
        {
            var useq = Config.Item("fq").GetValue<bool>();
            var usew = Config.Item("fw").GetValue<bool>();
            var minionCount = MinionManager.GetMinions(Player.Position, Q.Range, MinionTypes.All, MinionTeam.NotAlly);
            {
                foreach (var minion in minionCount)
                {
					if (useq && Q.IsReady()
                        && minion.IsValidTarget(Q.Range)
                        && minion.Health < Q.GetDamage(minion))
                    {
                        Q.CastOnUnit(minion);
                    }
					
					if (usew && W.IsReady()
                        && minion.IsValidTarget(W.Range)
                        && minion.Health < W.GetDamage(minion))
                    {
                        W.Cast();
                    }
					
					if ((useq && usew) && (Q.IsReady() && W.IsReady()) && (minion.IsValidTarget(Q.Range) && minion.IsValidTarget(W.Range)) && (minion.Health < (Q.GetDamage(minion) + W.GetDamage(minion))))
                    {
                        Q.CastOnUnit(minion);
						W.Cast();
                    }
                    
                }
            }
        }

        private static void harass()
        {
            var target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
            
            if (target == null || !target.IsValidTarget())
                return;
			
                if (E.IsReady() && Config.Item("hE").GetValue<bool>() && target.IsValidTarget(E.Range))
                {
                    CastE(target);
                }
                if (W.IsReady() && Config.Item("hW").GetValue<bool>() && target.IsValidTarget(W.Range))
                {
                    W.Cast();
                }
				if (Q.IsReady() && Config.Item("hQ").GetValue<bool>() && target.IsValidTarget(Q.Range))
                {
                    CastQ(target);
                }
        }

        private static void Clear()
        {
            var minionCount = MinionManager.GetMinions(Player.Position, Q.Range, MinionTypes.All, MinionTeam.NotAlly);
			var jungleCount = MinionManager.GetMinions(Player.Position, Q.Range, MinionTypes.All, MinionTeam.Neutral);
            {
                foreach (var minion in minionCount)
                {
					if (Config.Item("lE").GetValue<bool>() && E.IsReady() && minion.IsValidTarget(E.Range))
                    {
                        E.CastOnUnit(minion);
                    }
					
                    if (Config.Item("lQ").GetValue<bool>() && Q.IsReady() && minion.IsValidTarget(Q.Range))
                    {
                        Q.CastOnUnit(minion);
                    }                    
					
					if (Config.Item("lW").GetValue<bool>() && W.IsReady() && minion.IsValidTarget(W.Range))
                    {
                        W.Cast();
                    }

                }
				
				foreach (var jungle in jungleCount)
                {
					
                    if (Config.Item("jE").GetValue<bool>()  && E.IsReady() && jungle.IsValidTarget(E.Range))
                    {
                        E.CastOnUnit(jungle);
                    }				

                }
				
            }
        }
		
        private static void OnDraw(EventArgs args)
        {
            var Target = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);
        }
    }
}
