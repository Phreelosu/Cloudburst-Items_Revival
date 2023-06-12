﻿using System;
using Cloudburst.Characters;
using RoR2;
using RoR2.Stats;
using UnityEngine;
using UnityEngine.Networking;

namespace Cloudburst.Wyatt.Components
{
    public class WyattWalkmanBehavior : NetworkBehaviour, IOnDamageDealtServerReceiver
    {
        private CharacterBody characterBody;
        private ParticleSystem grooveEffect;
        private ParticleSystem grooveEffect2;
        private ChildLocator childLocator;
        private ParticleSystem flowEffect;
        private bool loseStacks { get { return stopwatch >= 3 && !flowing; } }

        private float stopwatch = 0;

        private float drainTimer = 0;

        [SyncVar]
        public bool flowing = false;

        private void Awake()
        {
            characterBody = base.GetComponent<CharacterBody>();
            childLocator = base.gameObject.GetComponentInChildren<ChildLocator>();

            grooveEffect = childLocator.FindChild("MusicEffect1").GetComponent<ParticleSystem>();
            grooveEffect2 = childLocator.FindChild("MusicEffect2").GetComponent<ParticleSystem>();

            flowEffect = childLocator.FindChild("MusicEffect3").GetComponent<ParticleSystem>();
        }

        //now I know why playing effects on the body is retared
        #region network Effects
        private void PlayGrooveEffectServer()
        {
            grooveEffect2.Play();
            grooveEffect.Play();
            RPCPlayGrooveEFfect();
        }
        [ClientRpc]
        private void RPCPlayGrooveEFfect()
        {
            grooveEffect2.Play();
            grooveEffect.Play();
        }

        private void PlayFlowEffectServer()
        {
            flowEffect.Play();
            RPCPlayFlowEFfect();
        }
        [ClientRpc]
        private void RPCPlayFlowEFfect()
        {
            flowEffect.Play();
        }

        private void StopFlowEffectServer()
        {
            flowEffect.Stop();
            RPCPStopFlowEFfect();
        }
        [ClientRpc]
        private void RPCPStopFlowEFfect()
        {
            flowEffect.Stop();
        }
        #endregion

        private void Start()
        {
            On.RoR2.CharacterBody.OnBuffFinalStackLost += CharacterBody_OnBuffFinalStackLost;
        }

        void OnDestroy() {
            On.RoR2.CharacterBody.OnBuffFinalStackLost -= CharacterBody_OnBuffFinalStackLost;
        }

        private void CharacterBody_OnBuffFinalStackLost(On.RoR2.CharacterBody.orig_OnBuffFinalStackLost orig, CharacterBody self, BuffDef buffDef)
        {
            if (flowing && NetworkServer.active && characterBody == self)
            {
                //flowing has stopped
                CCUtilities.SafeRemoveAllOfBuff(WyattSurvivor.instance.wyattCombatBuffDef, characterBody);
                flowing = false;
                StopFlowEffectServer();
            }
            orig(self, buffDef);
        }

        public void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                //fixedupdate but only on server
                ServerFixedUpdate();

            }
        }

        private void ServerFixedUpdate()
        {
            if (flowing == false)
            {
                stopwatch += Time.fixedDeltaTime;
                if (loseStacks)
                {
                    drainTimer += Time.fixedDeltaTime;
                    if (drainTimer >= 0.5f)
                    {
                        CCUtilities.SafeRemoveBuffs(WyattSurvivor.instance.wyattCombatBuffDef, characterBody, 2);
                        drainTimer = 0;
                    }
                }
            }
            
        }

        [Server]
        private void TriggerBehaviorInternal(float stacks)
        {
            var cap = 9 + stacks;
            if (characterBody && characterBody.GetBuffCount(WyattSurvivor.instance.wyattCombatBuffDef) < cap)
            {
                PlayGrooveEffectServer();
                /*EffectManager.SpawnEffect(Effects.wyattGrooveEffect, new EffectData()
                {
                    scale = 1,
                    origin = grooveEffect.transform.position
                }, true);*/
                characterBody.AddBuff(WyattSurvivor.instance.wyattCombatBuffDef);
                //characterBody.AddTimedBuff(Custodian.instance.wyattCombatDef, 3);
            }
            stopwatch = 0;
        }



        public void ActivateFlowAuthority()
        {
            if (NetworkServer.active)
            {
                ActivateFlowInternal();
                return;
            }
            CmdActivateFlow();
        }

        [Command]
        private void CmdActivateFlow()
        {
            ActivateFlowInternal();
        }

        [Server]
        private void ActivateFlowInternal()
        {
            int grooveCount = characterBody.GetBuffCount(WyattSurvivor.instance.wyattCombatBuffDef);
            float duration = 4;

            for (int i = 0; i < grooveCount; i++)
            {
                //add flow until we can't
                if (duration < 8)
                {
                    duration += 0.4f;
                }
            }

            characterBody.AddTimedBuff(WyattSurvivor.instance.wyattFlowBuffDef, duration);
            flowing = true;

            PlayFlowEffectServer();
        }

        public void OnDamageDealtServer(DamageReport damageReport)
        {
            if (damageReport.damageInfo?.inflictor == base.gameObject && flowing == false)
            {
                TriggerBehaviorInternal(1);
            }
        }
    }
}