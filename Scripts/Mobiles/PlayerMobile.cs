using System;
using System.Collections;
using System.Collections.Generic;
using Server.Misc;
using Server.Items;
using Server.Gumps;
using Server.Multis;
using Server.Engines.Help;
using Server.ContextMenus;
using Server.Network;
using Server.Spells;
using Server.Spells.Fifth;
using Server.Spells.Seventh;
using Server.Targeting;
using Server.Regions;
using Server.Accounting;
using Server.Engines.Craft;


namespace Server.Mobiles
{
	[Flags]
	public enum PlayerFlag // First 16 bits are reserved for default-distro use, start custom flags at 0x00010000
	{
		None					= 0x00000000,
		Glassblowing			= 0x00000001,
		Masonry					= 0x00000002,
		SandMining				= 0x00000004,
		StoneMining				= 0x00000008,
		ToggleMiningStone		= 0x00000010,
		KarmaLocked				= 0x00000020,
		AutoRenewInsurance		= 0x00000040,
		UseOwnFilter			= 0x00000080,
		PublicMyRunUO			= 0x00000100,
		PagingSquelched			= 0x00000200,
		Young					= 0x00000400,
		AcceptGuildInvites		= 0x00000800,
		DisplayChampionTitle	= 0x00001000,
		HasStatReward			= 0x00002000,
		RefuseTrades			= 0x00004000
	}

	public enum NpcGuild
	{
		None,
		MagesGuild,
		WarriorsGuild,
		ThievesGuild,
		RangersGuild,
		HealersGuild,
		MinersGuild,
		MerchantsGuild,
		TinkersGuild,
		TailorsGuild,
		FishermensGuild,
		BardsGuild,
		BlacksmithsGuild
	}

	public enum BlockMountType
	{
		None = -1,
		Dazed = 1040024,
		BolaRecovery = 1062910,
		DismountRecovery = 1070859
	}

    public partial class PlayerMobile : Mobile
    {
        private class CountAndTimeStamp
        {
            private int m_Count;
            private DateTime m_Stamp;

            public CountAndTimeStamp()
            {
            }

            public DateTime TimeStamp { get { return m_Stamp; } }
            public int Count
            {
                get { return m_Count; }
                set { m_Count = value; m_Stamp = DateTime.Now; }
            }
        }

        private NpcGuild m_NpcGuild;
        private DateTime m_NpcGuildJoinTime;
        private TimeSpan m_NpcGuildGameTime;
        private PlayerFlag m_Flags;
        private int m_StepsTaken;
        private int m_Profession;
        private bool m_IsStealthing; // IsStealthing should be moved to Server.Mobiles
        private bool m_IgnoreMobiles; // IgnoreMobiles should be moved to Server.Mobiles
                                      /*
                                       * a value of zero means, that the mobile is not executing the spell. Otherwise,
                                       * the value should match the BaseMana required
                                      */

        private DateTime m_LastOnline;
        private Server.Guilds.RankDefinition m_GuildRank;

        private int m_GuildMessageHue, m_AllianceMessageHue;

        private List<Mobile> m_AllFollowers;
        private List<Mobile> m_RecentlyReported;

        public List<Mobile> RecentlyReported
        {
            get
            {
                return m_RecentlyReported;
            }
            set
            {
                m_RecentlyReported = value;
            }
        }

        public List<Mobile> AllFollowers
        {
            get
            {
                if (m_AllFollowers == null)
                    m_AllFollowers = new List<Mobile>();
                return m_AllFollowers;
            }
        }

        public Server.Guilds.RankDefinition GuildRank
        {
            get
            {
                if (this.AccessLevel >= AccessLevel.GameMaster)
                    return Server.Guilds.RankDefinition.Leader;
                else
                    return m_GuildRank;
            }
            set { m_GuildRank = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int GuildMessageHue
        {
            get { return m_GuildMessageHue; }
            set { m_GuildMessageHue = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int AllianceMessageHue
        {
            get { return m_AllianceMessageHue; }
            set { m_AllianceMessageHue = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Profession
        {
            get { return m_Profession; }
            set { m_Profession = value; }
        }

        public int StepsTaken
        {
            get { return m_StepsTaken; }
            set { m_StepsTaken = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool IsStealthing // IsStealthing should be moved to Server.Mobiles
        {
            get { return m_IsStealthing; }
            set { m_IsStealthing = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool IgnoreMobiles // IgnoreMobiles should be moved to Server.Mobiles
        {
            get
            {
                return m_IgnoreMobiles;
            }
            set
            {
                if (m_IgnoreMobiles != value)
                {
                    m_IgnoreMobiles = value;
                    Delta(MobileDelta.Flags);
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public NpcGuild NpcGuild
        {
            get { return m_NpcGuild; }
            set { m_NpcGuild = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NpcGuildJoinTime
        {
            get { return m_NpcGuildJoinTime; }
            set { m_NpcGuildJoinTime = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastOnline
        {
            get { return m_LastOnline; }
            set { m_LastOnline = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan NpcGuildGameTime
        {
            get { return m_NpcGuildGameTime; }
            set { m_NpcGuildGameTime = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool PagingSquelched
        {
            get { return GetFlag(PlayerFlag.PagingSquelched); }
            set { SetFlag(PlayerFlag.PagingSquelched, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool KarmaLocked
        {
            get { return GetFlag(PlayerFlag.KarmaLocked); }
            set { SetFlag(PlayerFlag.KarmaLocked, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool PublicMyRunUO
        {
            get { return GetFlag(PlayerFlag.PublicMyRunUO); }
            set { SetFlag(PlayerFlag.PublicMyRunUO, value); InvalidateMyRunUO(); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool AcceptGuildInvites
        {
            get { return GetFlag(PlayerFlag.AcceptGuildInvites); }
            set { SetFlag(PlayerFlag.AcceptGuildInvites, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan DisguiseTimeLeft
        {
            get { return DisguiseTimers.TimeRemaining(this); }
        }

        private DateTime m_PeacedUntil;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime PeacedUntil
        {
            get { return m_PeacedUntil; }
            set { m_PeacedUntil = value; }
        }

        public static Direction GetDirection4(Point3D from, Point3D to)
        {
            int dx = from.X - to.X;
            int dy = from.Y - to.Y;

            int rx = dx - dy;
            int ry = dx + dy;

            Direction ret;

            if (rx >= 0 && ry >= 0)
                ret = Direction.West;
            else if (rx >= 0 && ry < 0)
                ret = Direction.South;
            else if (rx < 0 && ry < 0)
                ret = Direction.East;
            else
                ret = Direction.North;

            return ret;
        }

        public override bool OnDroppedItemToWorld(Item item, Point3D location)
        {
            if (!base.OnDroppedItemToWorld(item, location))
                return false;

            BounceInfo bi = item.GetBounce();

            if (bi != null)
            {
                Type type = item.GetType();

                if (type.IsDefined(typeof(FurnitureAttribute), true) || type.IsDefined(typeof(DynamicFlipingAttribute), true))
                {
                    object[] objs = type.GetCustomAttributes(typeof(FlipableAttribute), true);

                    if (objs != null && objs.Length > 0)
                    {
                        FlipableAttribute fp = objs[0] as FlipableAttribute;

                        if (fp != null)
                        {
                            int[] itemIDs = fp.ItemIDs;

                            Point3D oldWorldLoc = bi.m_WorldLoc;
                            Point3D newWorldLoc = location;

                            if (oldWorldLoc.X != newWorldLoc.X || oldWorldLoc.Y != newWorldLoc.Y)
                            {
                                Direction dir = GetDirection4(oldWorldLoc, newWorldLoc);

                                if (itemIDs.Length == 2)
                                {
                                    switch (dir)
                                    {
                                        case Direction.North:
                                        case Direction.South: item.ItemID = itemIDs[0]; break;
                                        case Direction.East:
                                        case Direction.West: item.ItemID = itemIDs[1]; break;
                                    }
                                }
                                else if (itemIDs.Length == 4)
                                {
                                    switch (dir)
                                    {
                                        case Direction.South: item.ItemID = itemIDs[0]; break;
                                        case Direction.East: item.ItemID = itemIDs[1]; break;
                                        case Direction.North: item.ItemID = itemIDs[2]; break;
                                        case Direction.West: item.ItemID = itemIDs[3]; break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

        public override int GetPacketFlags()
        {
            int flags = base.GetPacketFlags();

            if (m_IgnoreMobiles)
                flags |= 0x10;

            return flags;
        }

        public override int GetOldPacketFlags()
        {
            int flags = base.GetOldPacketFlags();

            if (m_IgnoreMobiles)
                flags |= 0x10;

            return flags;
        }

        public bool GetFlag(PlayerFlag flag)
        {
            return ((m_Flags & flag) != 0);
        }

        public void SetFlag(PlayerFlag flag, bool value)
        {
            if (value)
                m_Flags |= flag;
            else
                m_Flags &= ~flag;
        }

        public static void Initialize()
        {
            if (FastwalkPrevention)
                PacketHandlers.RegisterThrottler(0x02, new ThrottlePacketCallback(MovementThrottle_Callback));

            EventSink.Login += new LoginEventHandler(OnLogin);
            EventSink.Logout += new LogoutEventHandler(OnLogout);
            EventSink.Connected += new ConnectedEventHandler(EventSink_Connected);
            EventSink.Disconnected += new DisconnectedEventHandler(EventSink_Disconnected);
        }

        private MountBlock m_MountBlock;

        public BlockMountType MountBlockReason
        {
            get
            {
                return (CheckBlock(m_MountBlock)) ? m_MountBlock.m_Type : BlockMountType.None;
            }
        }

        private static bool CheckBlock(MountBlock block)
        {
            return ((block is MountBlock) && block.m_Timer.Running);
        }

        private class MountBlock
        {
            public BlockMountType m_Type;
            public Timer m_Timer;

            public MountBlock(TimeSpan duration, BlockMountType type, Mobile mobile)
            {
                m_Type = type;

                m_Timer = Timer.DelayCall(duration, new TimerStateCallback<Mobile>(RemoveBlock), mobile);
            }

            private void RemoveBlock(Mobile mobile)
            {
                (mobile as PlayerMobile).m_MountBlock = null;
            }
        }

        public void SetMountBlock(BlockMountType type, TimeSpan duration, bool dismount)
        {
            if (dismount)
            {
                if (this.Mount != null)
                {
                    this.Mount.Rider = null;
                }
            }

            if ((m_MountBlock == null) || !m_MountBlock.m_Timer.Running || (m_MountBlock.m_Timer.Next < (DateTime.UtcNow + duration)))
            {
                m_MountBlock = new MountBlock(duration, type, this);
            }
        }

        protected override void OnRaceChange(Race oldRace)
        {
            ValidateEquipment();
        }

        public override int MaxWeight { get { return (40 + (int)(3.5 * this.Str)); } }

        private int m_LastGlobalLight = -1, m_LastPersonalLight = -1;

        public override void OnNetStateChanged()
        {
            m_LastGlobalLight = -1;
            m_LastPersonalLight = -1;
        }

        public override void ComputeBaseLightLevels(out int global, out int personal)
        {
            global = LightCycle.ComputeLevelFor(this);
            personal = this.LightLevel;
        }

        public override void CheckLightLevels(bool forceResend)
        {
            NetState ns = this.NetState;

            if (ns == null)
                return;

            int global, personal;

            ComputeLightLevels(out global, out personal);

            if (!forceResend)
                forceResend = (global != m_LastGlobalLight || personal != m_LastPersonalLight);

            if (!forceResend)
                return;

            m_LastGlobalLight = global;
            m_LastPersonalLight = personal;

            ns.Send(GlobalLightLevel.Instantiate(global));
            ns.Send(new PersonalLightLevel(this, personal));
        }

        private static void OnLogin(LoginEventArgs e)
        {
            Mobile from = e.Mobile;

            if (AccountHandler.LockdownLevel > AccessLevel.Player)
            {
                string notice;

                Accounting.Account acct = from.Account as Accounting.Account;

                if (acct == null || !acct.HasAccess(from.NetState))
                {
                    if (from.AccessLevel == AccessLevel.Player)
                        notice = "The server is currently under lockdown. No players are allowed to log in at this time.";
                    else
                        notice = "The server is currently under lockdown. You do not have sufficient access level to connect.";

                    Timer.DelayCall(TimeSpan.FromSeconds(1.0), new TimerStateCallback(Disconnect), from);
                }
                else if (from.AccessLevel >= AccessLevel.Administrator)
                {
                    notice = "The server is currently under lockdown. As you are an administrator, you may change this from the [Admin gump.";
                }
                else
                {
                    notice = "The server is currently under lockdown. You have sufficient access level to connect.";
                }

                from.SendGump(new NoticeGump(1060637, 30720, notice, 0xFFC000, 300, 140, null, null));
                return;
            }
        }

        private bool m_NoDeltaRecursion;

        public void ValidateEquipment()
        {
            if (m_NoDeltaRecursion || Map == null || Map == Map.Internal)
                return;

            if (this.Items == null)
                return;

            m_NoDeltaRecursion = true;
            Timer.DelayCall(TimeSpan.Zero, new TimerCallback(ValidateEquipment_Sandbox));
        }

        private void ValidateEquipment_Sandbox()
        {
            try
            {
                if (Map == null || Map == Map.Internal)
                    return;

                List<Item> items = this.Items;

                if (items == null)
                    return;

                bool moved = false;

                int str = this.Str;
                int dex = this.Dex;
                int intel = this.Int;

                Mobile from = this;

                for (int i = items.Count - 1; i >= 0; --i)
                {
                    if (i >= items.Count)
                        continue;

                    Item item = items[i];

                    if (item is BaseWeapon)
                    {
                        BaseWeapon weapon = (BaseWeapon)item;

                        bool drop = false;

                        if (dex < weapon.DexRequirement)
                            drop = true;
                        else if (str < weapon.StrRequirement)
                            drop = true;
                        else if (intel < weapon.IntRequirement)
                            drop = true;
                        else if (weapon.RequiredRace != null && weapon.RequiredRace != this.Race)
                            drop = true;

                        if (drop)
                        {
                            string name = weapon.Name;

                            if (name == null)
                                name = String.Format("#{0}", weapon.LabelNumber);

                            from.SendLocalizedMessage(1062001, name); // You can no longer wield your ~1_WEAPON~
                            from.AddToBackpack(weapon);
                            moved = true;
                        }
                    }
                    else if (item is BaseArmor)
                    {
                        BaseArmor armor = (BaseArmor)item;

                        bool drop = false;

                        if (!armor.AllowMaleWearer && !from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (!armor.AllowFemaleWearer && from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (armor.RequiredRace != null && armor.RequiredRace != this.Race)
                        {
                            drop = true;
                        }
                        else
                        {
                            int strReq = armor.ComputeStatReq(StatType.Str);
                            int dexReq = armor.ComputeStatReq(StatType.Dex);
                            int intReq = armor.ComputeStatReq(StatType.Int);

                            if (dex < dexReq)
                                drop = true;
                            else if (str < strReq)
                                drop = true;
                            else if (intel < intReq)
                                drop = true;
                        }

                        if (drop)
                        {
                            string name = armor.Name;

                            if (name == null)
                                name = String.Format("#{0}", armor.LabelNumber);

                            if (armor is BaseShield)
                                from.SendLocalizedMessage(1062003, name); // You can no longer equip your ~1_SHIELD~
                            else
                                from.SendLocalizedMessage(1062002, name); // You can no longer wear your ~1_ARMOR~

                            from.AddToBackpack(armor);
                            moved = true;
                        }
                    }
                    else if (item is BaseClothing)
                    {
                        BaseClothing clothing = (BaseClothing)item;

                        bool drop = false;

                        if (!clothing.AllowMaleWearer && !from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (!clothing.AllowFemaleWearer && from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (clothing.RequiredRace != null && clothing.RequiredRace != this.Race)
                        {
                            drop = true;
                        }
                        else
                        {
                            int strBonus = clothing.ComputeStatBonus(StatType.Str);
                            int strReq = clothing.StrRequirement;

                            if (str < strReq || (str + strBonus) < 1)
                                drop = true;
                        }

                        if (drop)
                        {
                            string name = clothing.Name;

                            if (name == null)
                                name = String.Format("#{0}", clothing.LabelNumber);

                            from.SendLocalizedMessage(1062002, name); // You can no longer wear your ~1_ARMOR~

                            from.AddToBackpack(clothing);
                            moved = true;
                        }
                    }
                }

                if (moved)
                    from.SendLocalizedMessage(500647); // Some equipment has been moved to your backpack.
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                m_NoDeltaRecursion = false;
            }
        }

        public override void Delta(MobileDelta flag)
        {
            base.Delta(flag);

            if ((flag & MobileDelta.Stat) != 0)
                ValidateEquipment();

            if ((flag & (MobileDelta.Name | MobileDelta.Hue)) != 0)
                InvalidateMyRunUO();
        }

        private static void Disconnect(object state)
        {
            NetState ns = ((Mobile)state).NetState;

            if (ns != null)
                ns.Dispose();
        }

        private static void OnLogout(LogoutEventArgs e)
        {
        }

        private static void EventSink_Connected(ConnectedEventArgs e)
        {
            PlayerMobile pm = e.Mobile as PlayerMobile;

            if (pm != null)
            {
                pm.m_SessionStart = DateTime.Now;
                pm.BedrollLogout = false;
                pm.LastOnline = DateTime.Now;
            }

            DisguiseTimers.StartTimer(e.Mobile);
        }

        private static void EventSink_Disconnected(DisconnectedEventArgs e)
        {
            Mobile from = e.Mobile;

            PlayerMobile pm = e.Mobile as PlayerMobile;

            if (pm != null)
            {
                pm.m_GameTime += (DateTime.Now - pm.m_SessionStart);
                pm.m_SpeechLog = null;
                pm.LastOnline = DateTime.Now;
            }

            DisguiseTimers.StopTimer(from);
        }

        public override void RevealingAction()
        {
            Spells.Sixth.InvisibilitySpell.RemoveTimer(this);

            base.RevealingAction();

            m_IsStealthing = false; // IsStealthing should be moved to Server.Mobiles
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override bool Hidden
        {
            get
            {
                return base.Hidden;
            }
            set
            {
                base.Hidden = value;

                RemoveBuff(BuffIcon.Invisibility);  //Always remove, default to the hiding icon EXCEPT in the invis spell where it's explicitly set

                if (!Hidden)
                {
                    RemoveBuff(BuffIcon.HidingAndOrStealth);
                }
                else// if( !InvisibilitySpell.HasTimer( this ) )
                {
                    BuffInfo.AddBuff(this, new BuffInfo(BuffIcon.HidingAndOrStealth, 1075655)); //Hidden/Stealthing & You Are Hidden
                }
            }
        }

        public override void OnSubItemAdded(Item item)
        {
            if (AccessLevel < AccessLevel.GameMaster && item.IsChildOf(this.Backpack))
            {
                int maxWeight = WeightOverloading.GetMaxWeight(this);
                int curWeight = Mobile.BodyWeight + this.TotalWeight;

                if (curWeight > maxWeight)
                    this.SendLocalizedMessage(1019035, true, String.Format(" : {0} / {1}", curWeight, maxWeight));
            }
        }

        public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness)
        {
            if ((target is BaseCreature && ((BaseCreature)target).IsInvulnerable) || target is PlayerVendor || target is TownCrier)
            {
                if (message)
                {
                    if (target.Title == null)
                        SendMessage("{0} cannot be harmed.", target.Name);
                    else
                        SendMessage("{0} {1} cannot be harmed.", target.Name, target.Title);
                }

                return false;
            }

            return base.CanBeHarmful(target, message, ignoreOurBlessedness);
        }

        public override void OnItemAdded(Item item)
        {
            base.OnItemAdded(item);

            if (item is BaseArmor || item is BaseWeapon)
            {
                Hits = Hits; Stam = Stam; Mana = Mana;
            }

            if (this.NetState != null)
                CheckLightLevels(false);

            InvalidateMyRunUO();
        }

        public override void OnItemRemoved(Item item)
        {
            base.OnItemRemoved(item);

            if (item is BaseArmor || item is BaseWeapon)
            {
                Hits = Hits; Stam = Stam; Mana = Mana;
            }

            if (this.NetState != null)
                CheckLightLevels(false);

            InvalidateMyRunUO();
        }

        public override double ArmorRating
        {
            get
            {
                //BaseArmor ar;
                double rating = 0.0;

                AddArmorRating(ref rating, NeckArmor);
                AddArmorRating(ref rating, HandArmor);
                AddArmorRating(ref rating, HeadArmor);
                AddArmorRating(ref rating, ArmsArmor);
                AddArmorRating(ref rating, LegsArmor);
                AddArmorRating(ref rating, ChestArmor);
                AddArmorRating(ref rating, ShieldArmor);
                AddArmorRating(ref rating, ClothArmor);

                return VirtualArmor + VirtualArmorMod + rating;
            }
        }

        private void AddArmorRating(ref double rating, Item armor)
        {
            BaseArmor ar = armor as BaseArmor;

            if (ar != null)
                rating += ar.ArmorRatingScaled;

            BaseClothing bc = armor as BaseClothing;

            if (bc != null)
                rating += bc.ArmorRatingScaled;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int HitsMax
        {
            get
            {
                int strBase;
                int strOffs = GetStatOffset(StatType.Str);

                strBase = this.RawStr;

                return (strBase / 2) + 50 + strOffs;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int Str
        {
            get
            {
                return base.Str;
            }
            set
            {
                base.Str = value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int Int
        {
            get
            {
                return base.Int;
            }
            set
            {
                base.Int = value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int Dex
        {
            get
            {
                return base.Dex;
            }
            set
            {
                base.Dex = value;
            }
        }

        public override bool Move(Direction d)
        {
            NetState ns = this.NetState;

            if (ns != null)
            {
                if (HasGump(typeof(ResurrectGump))) {
                    if (Alive) {
                        CloseGump(typeof(ResurrectGump));
                    } else {
                        SendLocalizedMessage(500111); // You are frozen and cannot move.
                        return false;
                    }
                }
            }

            TimeSpan speed = ComputeMovementSpeed(d);

            bool res;

            if (!Alive)
                Server.Movement.MovementImpl.IgnoreMovableImpassables = true;

            res = base.Move(d);

            Server.Movement.MovementImpl.IgnoreMovableImpassables = false;

            if (!res)
                return false;

            m_NextMovementTime += speed;

            return true;
        }

        private bool m_LastProtectedMessage;
        private int m_NextProtectionCheck = 10;

        public virtual void RecheckTownProtection()
        {
            m_NextProtectionCheck = 10;

            Regions.GuardedRegion reg = (Regions.GuardedRegion)this.Region.GetRegion(typeof(Regions.GuardedRegion));
            bool isProtected = (reg != null && !reg.IsDisabled());

            if (isProtected != m_LastProtectedMessage)
            {
                if (isProtected)
                    SendLocalizedMessage(500112); // You are now under the protection of the town guards.
                else
                    SendLocalizedMessage(500113); // You have left the protection of the town guards.

                m_LastProtectedMessage = isProtected;
            }
        }

        public override void MoveToWorld(Point3D loc, Map map)
        {
            base.MoveToWorld(loc, map);

            RecheckTownProtection();
        }

        public override void SetLocation(Point3D loc, bool isTeleport)
        {
            if (!isTeleport && AccessLevel == AccessLevel.Player)
            {
                // moving, not teleporting
                int zDrop = (this.Location.Z - loc.Z);

                if (zDrop > 20) // we fell more than one story
                    Hits -= ((zDrop / 20) * 10) - 5; // deal some damage; does not kill, disrupt, etc
            }

            base.SetLocation(loc, isTeleport);

            if (isTeleport || --m_NextProtectionCheck == 0)
                RecheckTownProtection();
        }

        private delegate void ContextCallback();

        private class CallbackEntry : ContextMenuEntry
        {
            private ContextCallback m_Callback;

            public CallbackEntry(int number, ContextCallback callback) : this(number, -1, callback)
            {
            }

            public CallbackEntry(int number, int range, ContextCallback callback) : base(number, range)
            {
                m_Callback = callback;
            }

            public override void OnClick()
            {
                if (m_Callback != null)
                    m_Callback();
            }
        }

        public override void DisruptiveAction()
        {
            if (Meditating)
            {
                RemoveBuff(BuffIcon.ActiveMeditation);
            }

            base.DisruptiveAction();
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (this == from && !Warmode)
            {
                IMount mount = Mount;

                if (mount != null)
                    return;
            }

            base.OnDoubleClick(from);
        }

        public override bool CheckEquip(Item item)
        {
            if (!base.CheckEquip(item))
                return false;

            if (this.AccessLevel < AccessLevel.GameMaster && item.Layer != Layer.Mount && this.HasTrade)
            {
                BounceInfo bounce = item.GetBounce();

                if (bounce != null)
                {
                    if (bounce.m_Parent is Item)
                    {
                        Item parent = (Item)bounce.m_Parent;

                        if (parent == this.Backpack || parent.IsChildOf(this.Backpack))
                            return true;
                    }
                    else if (bounce.m_Parent == this)
                    {
                        return true;
                    }
                }

                SendLocalizedMessage(1004042); // You can only equip what you are already carrying while you have a trade pending.
                return false;
            }

            return true;
        }

        public override bool CheckTrade(Mobile to, Item item, SecureTradeContainer cont, bool message, bool checkItems, int plusItems, int plusWeight)
        {
            int msgNum = 0;

            if (cont == null)
            {
                if (to.Holding != null)
                    msgNum = 1062727; // You cannot trade with someone who is dragging something.
                else if (this.HasTrade)
                    msgNum = 1062781; // You are already trading with someone else!
                else if (to.HasTrade)
                    msgNum = 1062779; // That person is already involved in a trade
            }

            if (msgNum == 0)
            {
                if (cont != null)
                {
                    plusItems += cont.TotalItems;
                    plusWeight += cont.TotalWeight;
                }

                if (this.Backpack == null || !this.Backpack.CheckHold(this, item, false, checkItems, plusItems, plusWeight))
                    msgNum = 1004040; // You would not be able to hold this if the trade failed.
                else if (to.Backpack == null || !to.Backpack.CheckHold(to, item, false, checkItems, plusItems, plusWeight))
                    msgNum = 1004039; // The recipient of this trade would not be able to carry this.
                else
                    msgNum = CheckContentForTrade(item);
            }

            if (msgNum != 0)
            {
                if (message)
                {
                    if (msgNum == 1154111)
                        SendLocalizedMessage(msgNum, to.Name);
                    else
                        SendLocalizedMessage(msgNum);
                }

                return false;
            }

            return true;
        }

        private static int CheckContentForTrade(Item item)
        {
            if (item is TrapableContainer && ((TrapableContainer)item).TrapType != TrapType.None)
                return 1004044; // You may not trade trapped items.

            if (SkillHandlers.StolenItem.IsStolen(item))
                return 1004043; // You may not trade recently stolen items.

            if (item is Container)
            {
                foreach (Item subItem in item.Items)
                {
                    int msg = CheckContentForTrade(subItem);

                    if (msg != 0)
                        return msg;
                }
            }

            return 0;
        }

        public override bool CheckNonlocalDrop(Mobile from, Item item, Item target)
        {
            if (!base.CheckNonlocalDrop(from, item, target))
                return false;

            if (from.AccessLevel >= AccessLevel.GameMaster)
                return true;

            Container pack = this.Backpack;
            if (from == this && this.HasTrade && (target == pack || target.IsChildOf(pack)))
            {
                BounceInfo bounce = item.GetBounce();

                if (bounce != null && bounce.m_Parent is Item)
                {
                    Item parent = (Item)bounce.m_Parent;

                    if (parent == pack || parent.IsChildOf(pack))
                        return true;
                }

                SendLocalizedMessage(1004041); // You can't do that while you have a trade pending.
                return false;
            }

            return true;
        }

        protected override void OnLocationChange(Point3D oldLocation)
        {
            CheckLightLevels(false);
        }

        public override bool OnMoveOver(Mobile m)
        {
            if (m is BaseCreature && !((BaseCreature)m).Controlled)
                return (!Alive || !m.Alive || IsDeadBondedPet || m.IsDeadBondedPet) || (Hidden && AccessLevel > AccessLevel.Player);

            return base.OnMoveOver(m);
        }

        public override bool CheckShove(Mobile shoved)
        {
            if (m_IgnoreMobiles)
                return true;
            else
                return base.CheckShove(shoved);
        }

        public override void OnDamage(int amount, Mobile from, bool willKill)
        {
            int disruptThreshold = 0;

            if (amount > disruptThreshold)
            {
                BandageContext c = BandageContext.GetContext(this);

                if (c != null)
                    c.Slip();
            }

            WeightOverloading.FatigueOnDamage(this, amount);

            base.OnDamage(amount, from, willKill);
        }

        public override void Resurrect()
        {
            bool wasAlive = this.Alive;

            base.Resurrect();

            if (this.Alive && !wasAlive)
            {
                Item deathRobe = new DeathRobe();

                if (!EquipItem(deathRobe))
                    deathRobe.Delete();
            }
        }

        public override double RacialSkillBonus
        {
            get
            {
                return 0;
            }
        }

        private List<Item> m_EquipSnapshot;

        public List<Item> EquipSnapshot
        {
            get { return m_EquipSnapshot; }
        }

        private bool FindItems_Callback(Item item)
        {
            if (!item.Deleted && (item.LootType == LootType.Blessed || item.Insured))
            {
                if (this.Backpack != item.ParentEntity)
                {
                    return true;
                }
            }
            return false;
        }

        public override bool OnBeforeDeath()
        {
            NetState state = NetState;

            if (state != null)
                state.CancelAllTrades();

            DropHolding();

            m_EquipSnapshot = new List<Item>(this.Items);

            return base.OnBeforeDeath();
        }

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);

            m_EquipSnapshot = null;

            HueMod = -1;
            NameMod = null;
            SavagePaintExpiration = TimeSpan.Zero;

            SetHairMods(-1, -1);

            PolymorphSpell.StopTimer(this);
            IncognitoSpell.StopTimer(this);
            DisguiseTimers.RemoveTimer(this);

            EndAction(typeof(PolymorphSpell));
            EndAction(typeof(IncognitoSpell));

            SkillHandlers.StolenItem.ReturnOnDeath(c);

            if (m_PermaFlags.Count > 0)
            {
                m_PermaFlags.Clear();

                if (c is Corpse)
                    ((Corpse)c).Criminal = true;

                if (SkillHandlers.Stealing.ClassicMode)
                    Criminal = true;
            }

            Mobile killer = this.FindMostRecentDamager(true);

            if (killer is BaseCreature)
            {
                BaseCreature bc = (BaseCreature)killer;

                Mobile master = bc.GetMaster();
                if (master != null)
                    killer = master;
            }

            Server.Guilds.Guild.HandleDeath(this, killer);

            if (m_BuffTable != null)
            {
                List<BuffInfo> list = new List<BuffInfo>();

                foreach (BuffInfo buff in m_BuffTable.Values)
                {
                    if (!buff.RetainThroughDeath)
                    {
                        list.Add(buff);
                    }
                }

                for (int i = 0; i < list.Count; i++)
                {
                    RemoveBuff(list[i]);
                }
            }
        }

        private List<Mobile> m_PermaFlags;
        private List<Mobile> m_VisList;
        private Hashtable m_AntiMacroTable;
        private TimeSpan m_GameTime;
        private TimeSpan m_ShortTermElapse;
        private TimeSpan m_LongTermElapse;
        private DateTime m_SessionStart;
        private DateTime m_LastEscortTime;
        private DateTime m_SavagePaintExpiration;
        private SkillName m_Learning = (SkillName)(-1);

        public SkillName Learning
        {
            get { return m_Learning; }
            set { m_Learning = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan SavagePaintExpiration
        {
            get
            {
                TimeSpan ts = m_SavagePaintExpiration - DateTime.Now;

                if (ts < TimeSpan.Zero)
                    ts = TimeSpan.Zero;

                return ts;
            }
            set
            {
                m_SavagePaintExpiration = DateTime.Now + value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastEscortTime
        {
            get { return m_LastEscortTime; }
            set { m_LastEscortTime = value; }
        }

        public PlayerMobile()
        {
            m_VisList = new List<Mobile>();
            m_PermaFlags = new List<Mobile>();
            m_AntiMacroTable = new Hashtable();
            m_RecentlyReported = new List<Mobile>();

            m_GameTime = TimeSpan.Zero;
            m_ShortTermElapse = TimeSpan.FromHours(8.0);
            m_LongTermElapse = TimeSpan.FromHours(40.0);

            m_GuildRank = Guilds.RankDefinition.Lowest;

            InvalidateMyRunUO();
        }

        public override bool MutateSpeech(List<Mobile> hears, ref string text, ref object context)
        {
            if (Alive)
                return false;

            return base.MutateSpeech(hears, ref text, ref context);
        }

        private static void SendToStaffMessage(Mobile from, string text)
        {
            Packet p = null;

            foreach (NetState ns in from.GetClientsInRange(8))
            {
                Mobile mob = ns.Mobile;

                if (mob != null && mob.AccessLevel >= AccessLevel.GameMaster && mob.AccessLevel > from.AccessLevel)
                {
                    if (p == null)
                        p = Packet.Acquire(new UnicodeMessage(from.Serial, from.Body, MessageType.Regular, from.SpeechHue, 3, from.Language, from.Name, text));

                    ns.Send(p);
                }
            }

            Packet.Release(p);
        }

        private static void SendToStaffMessage(Mobile from, string format, params object[] args)
        {
            SendToStaffMessage(from, String.Format(format, args));
        }

        public override ApplyPoisonResult ApplyPoison(Mobile from, Poison poison)
        {
            if (!Alive)
                return ApplyPoisonResult.Immune;

            ApplyPoisonResult result = base.ApplyPoison(from, poison);

            if (from != null && result == ApplyPoisonResult.Poisoned && PoisonTimer is PoisonImpl.PoisonTimer)
                (PoisonTimer as PoisonImpl.PoisonTimer).From = from;

            return result;
        }

        public PlayerMobile(Serial s) : base(s)
        {
            m_VisList = new List<Mobile>();
            m_AntiMacroTable = new Hashtable();
            InvalidateMyRunUO();
        }

        public List<Mobile> VisibilityList
        {
            get { return m_VisList; }
        }

        public List<Mobile> PermaFlags
        {
            get { return m_PermaFlags; }
        }

        public override bool IsHarmfulCriminal(Mobile target)
        {
            if (SkillHandlers.Stealing.ClassicMode && target is PlayerMobile && ((PlayerMobile)target).m_PermaFlags.Count > 0)
            {
                int noto = Notoriety.Compute(this, target);

                if (noto == Notoriety.Innocent)
                    target.Delta(MobileDelta.Noto);

                return false;
            }

            if (target is BaseCreature && ((BaseCreature)target).InitialInnocent && !((BaseCreature)target).Controlled)
                return false;

            return base.IsHarmfulCriminal(target);
        }

        public bool AntiMacroCheck(Skill skill, object obj)
        {
            if (obj == null || m_AntiMacroTable == null || this.AccessLevel != AccessLevel.Player)
                return true;

            Hashtable tbl = (Hashtable)m_AntiMacroTable[skill];
            if (tbl == null)
                m_AntiMacroTable[skill] = tbl = new Hashtable();

            CountAndTimeStamp count = (CountAndTimeStamp)tbl[obj];
            if (count != null)
            {
                if (count.TimeStamp + SkillCheck.AntiMacroExpire <= DateTime.Now)
                {
                    count.Count = 1;
                    return true;
                }
                else
                {
                    ++count.Count;
                    if (count.Count <= SkillCheck.Allowance)
                        return true;
                    else
                        return false;
                }
            }
            else
            {
                tbl[obj] = count = new CountAndTimeStamp();
                count.Count = 1;

                return true;
            }
        }

        private void RevertHair()
        {
            SetHairMods(-1, -1);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            switch (version)
            {
                case 28:
                    {
                        m_PeacedUntil = reader.ReadDateTime();

                        goto case 27;
                    }
                case 27:
                case 26:
                case 25:
                case 24:
                case 23:
                case 22:
                case 21:
                case 20:
                    {
                        m_AllianceMessageHue = reader.ReadEncodedInt();
                        m_GuildMessageHue = reader.ReadEncodedInt();

                        goto case 19;
                    }
                case 19:
                    {
                        int rank = reader.ReadEncodedInt();
                        int maxRank = Guilds.RankDefinition.Ranks.Length - 1;
                        if (rank > maxRank)
                            rank = maxRank;

                        m_GuildRank = Guilds.RankDefinition.Ranks[rank];
                        m_LastOnline = reader.ReadDateTime();
                        goto case 18;
                    }
                case 18:
                case 17:
                case 16:
                    {
                        m_Profession = reader.ReadEncodedInt();
                        goto case 15;
                    }
                case 15:
                case 14:
                case 13:
                case 12:
                case 11:
                    {
                        if (version < 13)
                        {
                            List<Item> payed = reader.ReadStrongItemList();

                            for (int i = 0; i < payed.Count; ++i)
                                payed[i].PayedInsurance = true;
                        }

                        goto case 10;
                    }
                case 10:
                    {
                        if (reader.ReadBool())
                        {
                            m_HairModID = reader.ReadInt();
                            m_HairModHue = reader.ReadInt();
                            m_BeardModID = reader.ReadInt();
                            m_BeardModHue = reader.ReadInt();
                        }

                        goto case 9;
                    }
                case 9:
                    {
                        SavagePaintExpiration = reader.ReadTimeSpan();

                        if (SavagePaintExpiration > TimeSpan.Zero)
                        {
                            BodyMod = (Female ? 184 : 183);
                            HueMod = 0;
                        }

                        goto case 8;
                    }
                case 8:
                    {
                        m_NpcGuild = (NpcGuild)reader.ReadInt();
                        m_NpcGuildJoinTime = reader.ReadDateTime();
                        m_NpcGuildGameTime = reader.ReadTimeSpan();
                        goto case 7;
                    }
                case 7:
                    {
                        m_PermaFlags = reader.ReadStrongMobileList();
                        goto case 6;
                    }
                case 6:
                case 5:
                case 4:
                case 3:
                case 2:
                    {
                        m_Flags = (PlayerFlag)reader.ReadInt();
                        goto case 1;
                    }
                case 1:
                    {
                        m_LongTermElapse = reader.ReadTimeSpan();
                        m_ShortTermElapse = reader.ReadTimeSpan();
                        m_GameTime = reader.ReadTimeSpan();
                        goto case 0;
                    }
                case 0:
                    {
                        break;
                    }
			}

			if (m_RecentlyReported == null)
				m_RecentlyReported = new List<Mobile>();

			// Professions weren't verified on 1.0 RC0
			if ( !CharacterCreation.VerifyProfession( m_Profession ) )
				m_Profession = 0;

			if ( m_PermaFlags == null )
				m_PermaFlags = new List<Mobile>();

			if( m_GuildRank == null )
				m_GuildRank = Guilds.RankDefinition.Member;	//Default to member if going from older version to new version (only time it should be null)

			if( m_LastOnline == DateTime.MinValue && Account != null )
				m_LastOnline = ((Account)Account).LastLogin;

			if ( AccessLevel > AccessLevel.Player )
				m_IgnoreMobiles = true;

			List<Mobile> list = this.Stabled;

			for ( int i = 0; i < list.Count; ++i )
			{
				BaseCreature bc = list[i] as BaseCreature;

				if ( bc != null )
				{
					bc.IsStabled = true;
					bc.StabledBy = this;
				}
			}

			if( Hidden )	//Hiding is the only buff where it has an effect that's serialized.
				AddBuff( new BuffInfo( BuffIcon.HidingAndOrStealth, 1075655 ) );
		}

		public override void Serialize( GenericWriter writer )
		{
			//cleanup our anti-macro table
			foreach ( Hashtable t in m_AntiMacroTable.Values )
			{
				ArrayList remove = new ArrayList();
				foreach ( CountAndTimeStamp time in t.Values )
				{
					if ( time.TimeStamp + SkillCheck.AntiMacroExpire <= DateTime.Now )
						remove.Add( time );
				}

				for (int i=0;i<remove.Count;++i)
					t.Remove( remove[i] );
			}

			CheckKillDecay();

			base.Serialize( writer );

			writer.Write( (int) 28 ); // version

			writer.Write( (DateTime) m_PeacedUntil );

			writer.WriteEncodedInt( m_AllianceMessageHue );
			writer.WriteEncodedInt( m_GuildMessageHue );

			writer.WriteEncodedInt( m_GuildRank.Rank );
			writer.Write( m_LastOnline );

			writer.WriteEncodedInt( (int) m_Profession );

			bool useMods = ( m_HairModID != -1 || m_BeardModID != -1 );

			writer.Write( useMods );

			if ( useMods )
			{
				writer.Write( (int) m_HairModID );
				writer.Write( (int) m_HairModHue );
				writer.Write( (int) m_BeardModID );
				writer.Write( (int) m_BeardModHue );
			}

			writer.Write( SavagePaintExpiration );

			writer.Write( (int) m_NpcGuild );
			writer.Write( (DateTime) m_NpcGuildJoinTime );
			writer.Write( (TimeSpan) m_NpcGuildGameTime );

			writer.Write( m_PermaFlags, true );

			writer.Write( (int) m_Flags );

			writer.Write( m_LongTermElapse );
			writer.Write( m_ShortTermElapse );
			writer.Write( this.GameTime );
		}

		public void CheckKillDecay()
		{
			if ( m_ShortTermElapse < this.GameTime )
			{
				m_ShortTermElapse += TimeSpan.FromHours( 8 );
				if ( ShortTermMurders > 0 )
					--ShortTermMurders;
			}

			if ( m_LongTermElapse < this.GameTime )
			{
				m_LongTermElapse += TimeSpan.FromHours( 40 );
				if ( Kills > 0 )
					--Kills;
			}
		}

		public void ResetKillTime()
		{
			m_ShortTermElapse = this.GameTime + TimeSpan.FromHours( 8 );
			m_LongTermElapse = this.GameTime + TimeSpan.FromHours( 40 );
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public DateTime SessionStart
		{
			get{ return m_SessionStart; }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public TimeSpan GameTime
		{
			get
			{
				if ( NetState != null )
					return m_GameTime + (DateTime.Now - m_SessionStart);
				else
					return m_GameTime;
			}
		}

		public override bool CanSee( Mobile m )
		{
			if ( m is PlayerMobile && ((PlayerMobile)m).m_VisList.Contains( this ) )
				return true;

			return base.CanSee( m );
		}

		public virtual void CheckedAnimate( int action, int frameCount, int repeatCount, bool forward, bool repeat, int delay )
		{
			if( !Mounted )
			{
				base.Animate( action, frameCount, repeatCount, forward, repeat, delay );
			}
		}

		public override void Animate( int action, int frameCount, int repeatCount, bool forward, bool repeat, int delay )
		{
			base.Animate( action, frameCount, repeatCount, forward, repeat, delay );
		}

		public override void OnAfterDelete()
		{
			base.OnAfterDelete();

			BaseHouse.HandleDeletion( this );

			DisguiseTimers.RemoveTimer( this );
		}

		private bool m_BedrollLogout;

		public bool BedrollLogout
		{
			get{ return m_BedrollLogout; }
			set{ m_BedrollLogout = value; }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public override bool Paralyzed
		{
			get
			{
				return base.Paralyzed;
			}
			set
			{
				base.Paralyzed = value;

				if( value )
					AddBuff( new BuffInfo( BuffIcon.Paralyze, 1075827 ) );	//Paralyze/You are frozen and can not move
				else
					RemoveBuff( BuffIcon.Paralyze );
			}
		}

        public override TimeSpan GetLogoutDelay()
        {
            if (BedrollLogout || TestCenter.Enabled)
                return TimeSpan.Zero;

            return base.GetLogoutDelay();
        }

        #region MyRunUO Invalidation
        private bool m_ChangedMyRunUO;

		public bool ChangedMyRunUO
		{
			get{ return m_ChangedMyRunUO; }
			set{ m_ChangedMyRunUO = value; }
		}

		public void InvalidateMyRunUO()
		{
			if ( !Deleted && !m_ChangedMyRunUO )
			{
				m_ChangedMyRunUO = true;
				Engines.MyRunUO.MyRunUO.QueueMobileUpdate( this );
			}
		}

		public override void OnGenderChanged( bool oldFemale )
		{
			InvalidateMyRunUO();
		}

		public override void OnGuildChange( Server.Guilds.BaseGuild oldGuild )
		{
			InvalidateMyRunUO();
		}

		public override void OnGuildTitleChange( string oldTitle )
		{
			InvalidateMyRunUO();
		}

		public override void OnKarmaChange( int oldValue )
		{
			InvalidateMyRunUO();
		}

		public override void OnFameChange( int oldValue )
		{
			InvalidateMyRunUO();
		}

		public override void OnAccessLevelChanged( AccessLevel oldLevel )
		{
			if ( AccessLevel == AccessLevel.Player )
				IgnoreMobiles = false;
			else
				IgnoreMobiles = true;

			InvalidateMyRunUO();
		}

		public override void OnRawStatChange( StatType stat, int oldValue )
		{
			InvalidateMyRunUO();
		}

		public override void OnDelete()
		{
			InvalidateMyRunUO();
		}

		#endregion

		#region Fastwalk Prevention
		private static bool FastwalkPrevention = true; // Is fastwalk prevention enabled?
		private static TimeSpan FastwalkThreshold = TimeSpan.FromSeconds( 0.4 ); // Fastwalk prevention will become active after 0.4 seconds

		private DateTime m_NextMovementTime;

		public virtual bool UsesFastwalkPrevention{ get{ return ( AccessLevel < AccessLevel.Counselor ); } }

		public override TimeSpan ComputeMovementSpeed( Direction dir, bool checkTurning )
		{
			if ( checkTurning && (dir & Direction.Mask) != (this.Direction & Direction.Mask) )
				return Mobile.RunMount;	// We are NOT actually moving (just a direction change)

			TransformContext context = TransformationSpellHelper.GetContext( this );

			if (false && context != null )
				return Mobile.WalkFoot;

			bool running = ( (dir & Direction.Running) != 0 );

			bool onHorse = ( this.Mount != null );
            
			if( onHorse )
				return ( running ? Mobile.RunMount : Mobile.WalkMount );

			return ( running ? Mobile.RunFoot : Mobile.WalkFoot );
		}

		public static bool MovementThrottle_Callback( NetState ns )
		{
			PlayerMobile pm = ns.Mobile as PlayerMobile;

			if ( pm == null || !pm.UsesFastwalkPrevention )
				return true;

			if ( pm.m_NextMovementTime == DateTime.MinValue )
			{
				// has not yet moved
				pm.m_NextMovementTime = DateTime.Now;
				return true;
			}

			TimeSpan ts = pm.m_NextMovementTime - DateTime.Now;

			if ( ts < TimeSpan.Zero )
			{
				// been a while since we've last moved
				pm.m_NextMovementTime = DateTime.Now;
				return true;
			}

			return ( ts < FastwalkThreshold );
		}

		#endregion

		#region Enemy of One
		private Type m_EnemyOfOneType;
		private bool m_WaitingForEnemy;

		public Type EnemyOfOneType
		{
			get{ return m_EnemyOfOneType; }
			set
			{
				Type oldType = m_EnemyOfOneType;
				Type newType = value;

				if ( oldType == newType )
					return;

				m_EnemyOfOneType = value;

				DeltaEnemies( oldType, newType );
			}
		}

		public bool WaitingForEnemy
		{
			get{ return m_WaitingForEnemy; }
			set{ m_WaitingForEnemy = value; }
		}

		private void DeltaEnemies( Type oldType, Type newType )
		{
			foreach ( Mobile m in this.GetMobilesInRange( 18 ) )
			{
				Type t = m.GetType();

				if ( t == oldType || t == newType ) {
					NetState ns = this.NetState;

					if ( ns != null ) {
						if ( ns.StygianAbyss ) {
							ns.Send( new MobileMoving( m, Notoriety.Compute( this, m ) ) );
						} else {
							ns.Send( new MobileMovingOld( m, Notoriety.Compute( this, m ) ) );
						}
					}
				}
			}
		}

		#endregion

		#region Hair and beard mods
		private int m_HairModID = -1, m_HairModHue;
		private int m_BeardModID = -1, m_BeardModHue;

		public void SetHairMods( int hairID, int beardID )
		{
			if ( hairID == -1 )
				InternalRestoreHair( true, ref m_HairModID, ref m_HairModHue );
			else if ( hairID != -2 )
				InternalChangeHair( true, hairID, ref m_HairModID, ref m_HairModHue );

			if ( beardID == -1 )
				InternalRestoreHair( false, ref m_BeardModID, ref m_BeardModHue );
			else if ( beardID != -2 )
				InternalChangeHair( false, beardID, ref m_BeardModID, ref m_BeardModHue );
		}

		private void CreateHair( bool hair, int id, int hue )
		{
			if( hair )
			{
				//TODO Verification?
				HairItemID = id;
				HairHue = hue;
			}
			else
			{
				FacialHairItemID = id;
				FacialHairHue = hue;
			}
		}

		private void InternalRestoreHair( bool hair, ref int id, ref int hue )
		{
			if ( id == -1 )
				return;

			if ( hair )
				HairItemID = 0;
			else
				FacialHairItemID = 0;

			//if( id != 0 )
			CreateHair( hair, id, hue );

			id = -1;
			hue = 0;
		}

		private void InternalChangeHair( bool hair, int id, ref int storeID, ref int storeHue )
		{
			if ( storeID == -1 )
			{
				storeID = hair ? HairItemID : FacialHairItemID;
				storeHue = hair ? HairHue : FacialHairHue;
			}
			CreateHair( hair, id, 0 );
		}

		#endregion

		#region Speech log
		private SpeechLog m_SpeechLog;

		public SpeechLog SpeechLog{ get{ return m_SpeechLog; } }

		public override void OnSpeech( SpeechEventArgs e )
		{
			if ( SpeechLog.Enabled && this.NetState != null )
			{
				if ( m_SpeechLog == null )
					m_SpeechLog = new SpeechLog();

				m_SpeechLog.Add( e.Mobile, e.Speech );
			}
		}

		#endregion

		#region Buff Icons

		public void ResendBuffs()
		{
			if( !BuffInfo.Enabled || m_BuffTable == null )
				return;

			NetState state = this.NetState;

			if( state != null && state.BuffIcon )
			{
				foreach( BuffInfo info in m_BuffTable.Values )
				{
					state.Send( new AddBuffPacket( this, info ) );
				}
			}
		}

		private Dictionary<BuffIcon, BuffInfo> m_BuffTable;

		public void AddBuff( BuffInfo b )
		{
			if( !BuffInfo.Enabled || b == null )
				return;

			RemoveBuff( b );	//Check & subsequently remove the old one.

			if( m_BuffTable == null )
				m_BuffTable = new Dictionary<BuffIcon, BuffInfo>();

			m_BuffTable.Add( b.ID, b );

			NetState state = this.NetState;

			if( state != null && state.BuffIcon )
			{
				state.Send( new AddBuffPacket( this, b ) );
			}
		}

		public void RemoveBuff( BuffInfo b )
		{
			if( b == null )
				return;

			RemoveBuff( b.ID );
		}

		public void RemoveBuff( BuffIcon b )
		{
			if( m_BuffTable == null || !m_BuffTable.ContainsKey( b ) )
				return;

			BuffInfo info = m_BuffTable[b];

			if( info.Timer != null && info.Timer.Running )
				info.Timer.Stop();

			m_BuffTable.Remove( b );

			NetState state = this.NetState;

			if( state != null && state.BuffIcon )
			{
				state.Send( new RemoveBuffPacket( this, b ) );
			}

			if( m_BuffTable.Count <= 0 )
				m_BuffTable = null;
		}

		#endregion
	}
}