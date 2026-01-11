using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Treaty types for BLT diplomacy system
    /// </summary>
    public enum TreatyType
    {
        Truce,
        NAP,
        Alliance,
        Tribute
    }

    /// <summary>
    /// Base class for all treaties between kingdoms
    /// </summary>
    public abstract class BLTTreaty
    {
        public string Kingdom1Id { get; set; }
        public string Kingdom2Id { get; set; }
        public CampaignTime StartDate { get; set; }
        public abstract TreatyType Type { get; }

        protected BLTTreaty() { }

        protected BLTTreaty(Kingdom k1, Kingdom k2)
        {
            Kingdom1Id = k1?.StringId;
            Kingdom2Id = k2?.StringId;
            StartDate = CampaignTime.Now;
        }

        public Kingdom GetKingdom1() => Kingdom.All.FirstOrDefault(k => k.StringId == Kingdom1Id);
        public Kingdom GetKingdom2() => Kingdom.All.FirstOrDefault(k => k.StringId == Kingdom2Id);

        public bool InvolvesBoth(Kingdom k1, Kingdom k2)
        {
            return (Kingdom1Id == k1?.StringId && Kingdom2Id == k2?.StringId) ||
                   (Kingdom1Id == k2?.StringId && Kingdom2Id == k1?.StringId);
        }

        public bool Involves(Kingdom k)
        {
            return Kingdom1Id == k?.StringId || Kingdom2Id == k?.StringId;
        }

        public Kingdom GetOtherKingdom(Kingdom k)
        {
            if (Kingdom1Id == k?.StringId) return GetKingdom2();
            if (Kingdom2Id == k?.StringId) return GetKingdom1();
            return null;
        }
    }

    /// <summary>
    /// Truce - temporary peace period that blocks war declarations
    /// </summary>
    public class BLTTruce : BLTTreaty
    {
        public override TreatyType Type => TreatyType.Truce;
        public CampaignTime ExpirationDate { get; set; }

        public BLTTruce() { }

        public BLTTruce(Kingdom k1, Kingdom k2, int durationDays) : base(k1, k2)
        {
            ExpirationDate = CampaignTime.DaysFromNow(durationDays);
        }

        public bool IsExpired() => CampaignTime.Now >= ExpirationDate;
        public int DaysRemaining() => (int)(ExpirationDate - CampaignTime.Now).ToDays;
    }

    /// <summary>
    /// Non-Aggression Pact - mutual agreement not to declare war
    /// </summary>
    public class BLTNAP : BLTTreaty
    {
        public override TreatyType Type => TreatyType.NAP;

        public BLTNAP() { }
        public BLTNAP(Kingdom k1, Kingdom k2) : base(k1, k2) { }
    }

    /// <summary>
    /// Alliance - defensive pact with optional offensive calls to war
    /// </summary>
    public class BLTAlliance : BLTTreaty
    {
        public override TreatyType Type => TreatyType.Alliance;

        public BLTAlliance() { }
        public BLTAlliance(Kingdom k1, Kingdom k2) : base(k1, k2) { }
    }

    /// <summary>
    /// Tribute - daily gold transfer from one kingdom to another
    /// </summary>
    public class BLTTribute : BLTTreaty
    {
        public override TreatyType Type => TreatyType.Tribute;
        public string PayerKingdomId { get; set; }
        public int DailyAmount { get; set; }
        public int RemainingDays { get; set; }

        public BLTTribute() { }

        public BLTTribute(Kingdom payer, Kingdom receiver, int dailyAmount, int durationDays) : base(payer, receiver)
        {
            PayerKingdomId = payer?.StringId;
            DailyAmount = dailyAmount;
            RemainingDays = durationDays;
        }

        public Kingdom GetPayer() => Kingdom.All.FirstOrDefault(k => k.StringId == PayerKingdomId);
        public Kingdom GetReceiver() => GetOtherKingdom(GetPayer());

        public bool IsExpired() => RemainingDays <= 0;
    }

    /// <summary>
    /// Call to War proposal - offensive war invitation with acceptance window
    /// </summary>
    public class BLTCTWProposal
    {
        public string ProposerKingdomId { get; set; }
        public string CalledKingdomId { get; set; }
        public string TargetKingdomId { get; set; }
        public CampaignTime ExpirationDate { get; set; }

        public BLTCTWProposal() { }

        public BLTCTWProposal(Kingdom proposer, Kingdom called, Kingdom target, int daysToAccept)
        {
            ProposerKingdomId = proposer?.StringId;
            CalledKingdomId = called?.StringId;
            TargetKingdomId = target?.StringId;
            ExpirationDate = CampaignTime.DaysFromNow(daysToAccept);
        }

        public Kingdom GetProposer() => Kingdom.All.FirstOrDefault(k => k.StringId == ProposerKingdomId);
        public Kingdom GetCalled() => Kingdom.All.FirstOrDefault(k => k.StringId == CalledKingdomId);
        public Kingdom GetTarget() => Kingdom.All.FirstOrDefault(k => k.StringId == TargetKingdomId);

        public bool IsExpired() => CampaignTime.Now >= ExpirationDate;
        public int DaysRemaining() => Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }

    /// <summary>
    /// BLT War tracking - separate from game wars, tracks main participants and assisting allies
    /// </summary>
    public class BLTWar
    {
        public string Attacker1Id { get; set; }
        public string Defender1Id { get; set; }
        public List<string> Attacker1AlliesIds { get; set; } = new List<string>();
        public List<string> Defender1AlliesIds { get; set; } = new List<string>();
        public CampaignTime StartDate { get; set; }

        public BLTWar() { }

        public BLTWar(Kingdom attacker, Kingdom defender)
        {
            Attacker1Id = attacker?.StringId;
            Defender1Id = defender?.StringId;
            StartDate = CampaignTime.Now;
        }

        public Kingdom GetAttacker() => Kingdom.All.FirstOrDefault(k => k.StringId == Attacker1Id);
        public Kingdom GetDefender() => Kingdom.All.FirstOrDefault(k => k.StringId == Defender1Id);

        public List<Kingdom> GetAttackerAllies() => Attacker1AlliesIds
            .Select(id => Kingdom.All.FirstOrDefault(k => k.StringId == id))
            .Where(k => k != null)
            .ToList();

        public List<Kingdom> GetDefenderAllies() => Defender1AlliesIds
            .Select(id => Kingdom.All.FirstOrDefault(k => k.StringId == id))
            .Where(k => k != null)
            .ToList();

        public bool IsMainParticipant(Kingdom k)
        {
            return k?.StringId == Attacker1Id || k?.StringId == Defender1Id;
        }

        public bool IsAttackerSide(Kingdom k)
        {
            return k?.StringId == Attacker1Id || Attacker1AlliesIds.Contains(k?.StringId);
        }

        public bool IsDefenderSide(Kingdom k)
        {
            return k?.StringId == Defender1Id || Defender1AlliesIds.Contains(k?.StringId);
        }

        public bool Involves(Kingdom k)
        {
            return IsAttackerSide(k) || IsDefenderSide(k);
        }

        public void AddAttackerAlly(Kingdom k)
        {
            if (k != null && !Attacker1AlliesIds.Contains(k.StringId))
                Attacker1AlliesIds.Add(k.StringId);
        }

        public void AddDefenderAlly(Kingdom k)
        {
            if (k != null && !Defender1AlliesIds.Contains(k.StringId))
                Defender1AlliesIds.Add(k.StringId);
        }

        public void RemoveAlly(Kingdom k)
        {
            Attacker1AlliesIds.Remove(k?.StringId);
            Defender1AlliesIds.Remove(k?.StringId);
        }

        public List<Kingdom> GetEnemies(Kingdom k)
        {
            if (IsAttackerSide(k))
            {
                var enemies = new List<Kingdom> { GetDefender() };
                enemies.AddRange(GetDefenderAllies());
                return enemies.Where(e => e != null).ToList();
            }
            else if (IsDefenderSide(k))
            {
                var enemies = new List<Kingdom> { GetAttacker() };
                enemies.AddRange(GetAttackerAllies());
                return enemies.Where(e => e != null).ToList();
            }
            return new List<Kingdom>();
        }

        public Kingdom GetMainOpponent(Kingdom k)
        {
            if (k?.StringId == Attacker1Id) return GetDefender();
            if (k?.StringId == Defender1Id) return GetAttacker();
            if (IsAttackerSide(k)) return GetDefender();
            if (IsDefenderSide(k)) return GetAttacker();
            return null;
        }
    }
}