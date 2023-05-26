using BepInEx;
using BepInEx.Configuration;
using MysticsRisky2Utils;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using RoR2.ContentManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
namespace ExtraChallengeShrines
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInDependency(MysticsRisky2UtilsPlugin.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    public class ExtraChallengeShrinesPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.themysticsword.extrachallengeshrines";
        public const string PluginName = "Extra Challenge Shrines";
        public const string PluginVersion = "1.0.2";

        public static System.Reflection.Assembly executingAssembly;
        public static string pluginLocation;

        private static AssetBundle _assetBundle;
        public static AssetBundle AssetBundle
        {
            get
            {
                if (_assetBundle == null)
                    _assetBundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(pluginLocation), "extrachallengeshrinesassetbundle"));
                return _assetBundle;
            }
        }

        public static ConfigFile config;
        public static ConfigEntry<bool> ignoreBalanceChanges;

        public void Awake()
        {
            pluginLocation = Info.Location;
            executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            config = Config;

            NetworkingAPI.RegisterMessageType<ExtraChallengeShrinesTeleporterComponent.SyncShrineStacks>();

            ConfigOptions.ConfigurableValue.CreateBool(
                PluginGUID,
                PluginName,
                config,
                "General",
                "Debug",
                false,
                "Make all challenge shrines spawn on each stage",
                onChanged: (x) =>
                {
                    if (x) GenericGameEvents.OnPopulateScene += DebugGuaranteeAllShrines;
                    else GenericGameEvents.OnPopulateScene -= DebugGuaranteeAllShrines;
                }
            );
            ignoreBalanceChanges = ConfigOptions.ConfigurableValue.CreateBool(
                PluginGUID,
                PluginName,
                config,
                "General",
                "Ignore Balance Changes",
                true,
                "If true, most of the values in the config will be ignored and will use default values. Set to false if you want to fully customize your mod experience."
            ).bepinexConfigEntry;

            MysticsRisky2Utils.ContentManagement.ContentLoadHelper.PluginAwakeLoad<MysticsRisky2Utils.BaseAssetTypes.BaseItem>(executingAssembly);
            MysticsRisky2Utils.ContentManagement.ContentLoadHelper.PluginAwakeLoad<MysticsRisky2Utils.BaseAssetTypes.BaseEquipment>(executingAssembly);
            MysticsRisky2Utils.ContentManagement.ContentLoadHelper.PluginAwakeLoad<MysticsRisky2Utils.BaseAssetTypes.BaseBuff>(executingAssembly);
            MysticsRisky2Utils.ContentManagement.ContentLoadHelper.PluginAwakeLoad<MysticsRisky2Utils.BaseAssetTypes.BaseInteractable>(executingAssembly);
            MysticsRisky2Utils.ContentManagement.ContentLoadHelper.PluginAwakeLoad<MysticsRisky2Utils.BaseAssetTypes.BaseCharacterBody>(executingAssembly);
            MysticsRisky2Utils.ContentManagement.ContentLoadHelper.PluginAwakeLoad<MysticsRisky2Utils.BaseAssetTypes.BaseCharacterMaster>(executingAssembly);
            MysticsRisky2Utils.ContentManagement.ContentLoadHelper.PluginAwakeLoad<BlankLoadableAsset>(executingAssembly);

            ContentManager.collectContentPackProviders += (addContentPackProvider) =>
            {
                addContentPackProvider(new ExtraChallengeShrinesContent());
            };

            On.RoR2.TeleporterInteraction.Start += TeleporterInteraction_Start;
            TeleporterInteraction.onTeleporterBeginChargingGlobal += TeleporterInteraction_onTeleporterBeginChargingGlobal;
            On.RoR2.TeleporterInteraction.IdleState.OnInteractionBegin += IdleState_OnInteractionBegin;
            On.RoR2.BossGroup.OnMemberAddedServer += BossGroup_OnMemberAddedServer;
            On.RoR2.BossGroup.DropRewards += BossGroup_DropRewards;
            On.RoR2.CombatDirector.SetNextSpawnAsBoss += CombatDirector_SetNextSpawnAsBoss;

            RoR2Application.onLoad += () =>
            {
                ExtraChallengeShrinesTeleporterComponent.rockShrineDropTable = Addressables.LoadAssetAsync<PickupDropTable>("RoR2/Base/Common/dtTier1Item.asset").WaitForCompletion();
            };
        }

        private void DebugGuaranteeAllShrines(Xoroshiro128Plus rng)
        {
            DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(Interactables.ShrineCrown.spawnCard, new DirectorPlacementRule
            {
                placementMode = DirectorPlacementRule.PlacementMode.Random
            }, rng));
            DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(Interactables.ShrineRock.spawnCard, new DirectorPlacementRule
            {
                placementMode = DirectorPlacementRule.PlacementMode.Random
            }, rng));
            DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(Interactables.ShrineEye.spawnCard, new DirectorPlacementRule
            {
                placementMode = DirectorPlacementRule.PlacementMode.Random
            }, rng));
            DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(Addressables.LoadAssetAsync<InteractableSpawnCard>("RoR2/Base/ShrineBoss/iscShrineBoss.asset").WaitForCompletion(), new DirectorPlacementRule
            {
                placementMode = DirectorPlacementRule.PlacementMode.Random
            }, rng));
        }

        private void TeleporterInteraction_Start(On.RoR2.TeleporterInteraction.orig_Start orig, TeleporterInteraction self)
        {
            orig(self);
            var tpComponent = self.gameObject.AddComponent<ExtraChallengeShrinesTeleporterComponent>();

            GameObject MakeNewIndicator(Material material)
            {
                var newIndicator = Instantiate(self.bossShrineIndicator, self.bossShrineIndicator.transform.parent);
                newIndicator.GetComponentInChildren<Renderer>().material = material;
                newIndicator.SetActive(false);
                return newIndicator;
            }

            tpComponent.crownShrineIndicator = MakeNewIndicator(ExtraChallengeShrinesTeleporterComponent.crownShrineIndicatorMaterial);
            tpComponent.crownShrineIndicator.transform.position += 4f * Vector3.up;

            tpComponent.rockShrineIndicator = MakeNewIndicator(ExtraChallengeShrinesTeleporterComponent.rockShrineIndicatorMaterial);
            tpComponent.rockShrineIndicator.transform.position -= 2f * Vector3.up;

            tpComponent.eyeShrineIndicator = MakeNewIndicator(ExtraChallengeShrinesTeleporterComponent.eyeShrineIndicatorMaterial);
            tpComponent.eyeShrineIndicator.transform.position += 2f * Vector3.up;
        }

        private void TeleporterInteraction_onTeleporterBeginChargingGlobal(TeleporterInteraction self)
        {
            if (NetworkServer.active)
            {
                var tpComponent = self.GetComponent<ExtraChallengeShrinesTeleporterComponent>();
                if (!tpComponent) return;
                if (self.bossDirector)
                {
                    void TryAddCredits(int stackCount, float onFirstStack, float onExtraStacks)
                    {
                        if (stackCount > 0)
                        {
                            var creditsToAdd = onFirstStack + onExtraStacks * (stackCount - 1);
                            self.bossDirector.monsterCredit += (int)(creditsToAdd * Mathf.Pow(Run.instance.compensatedDifficultyCoefficient, 0.5f));
                        }
                    }
                    TryAddCredits(tpComponent.crownShrineStacks, Interactables.ShrineCrown.bossCredits, Interactables.ShrineCrown.bossCreditsPerStack);
                    TryAddCredits(tpComponent.rockShrineStacks, Interactables.ShrineRock.bossCredits, Interactables.ShrineRock.bossCreditsPerStack);
                    TryAddCredits(tpComponent.eyeShrineStacks, Interactables.ShrineEye.bossCredits, Interactables.ShrineEye.bossCreditsPerStack);
                }
            }
        }

        private void IdleState_OnInteractionBegin(On.RoR2.TeleporterInteraction.IdleState.orig_OnInteractionBegin orig, EntityStates.BaseState self, Interactor activator)
        {
            orig(self, activator);
            var tpComponent = ((TeleporterInteraction.IdleState)self).teleporterInteraction.GetComponent<ExtraChallengeShrinesTeleporterComponent>();
            if (tpComponent)
            {
                if (tpComponent.crownShrineStacks > 0)
                {
                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                    {
                        baseToken = "EXTRACHALLENGESHRINES_SHRINE_CROWN_BEGIN_TRIAL"
                    });
                }
                if (tpComponent.rockShrineStacks > 0)
                {
                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                    {
                        baseToken = "EXTRACHALLENGESHRINES_SHRINE_ROCK_BEGIN_TRIAL"
                    });
                }
                if (tpComponent.eyeShrineStacks > 0)
                {
                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                    {
                        baseToken = "EXTRACHALLENGESHRINES_SHRINE_EYE_BEGIN_TRIAL"
                    });
                }
            }
        }

        private void BossGroup_OnMemberAddedServer(On.RoR2.BossGroup.orig_OnMemberAddedServer orig, BossGroup self, CharacterMaster memberMaster)
        {
            orig(self, memberMaster);
            var tpComponent = self.GetComponent<ExtraChallengeShrinesTeleporterComponent>();
            if (tpComponent)
            {
                if (tpComponent.crownShrineStacks > 0)
                {
                    if (tpComponent.crownSelectedElite == null) tpComponent.RollCrownElite();
                    if (tpComponent.crownSelectedElite != null)
                    {
                        if (memberMaster.inventory.GetEquipmentIndex() == EquipmentIndex.None && tpComponent.crownSelectedElite.eliteEquipmentDef)
                        {
                            memberMaster.inventory.SetEquipmentIndex(tpComponent.crownSelectedElite.eliteEquipmentDef.equipmentIndex);
                        }
                        memberMaster.inventory.GiveItem(RoR2Content.Items.BoostHp, Mathf.RoundToInt((tpComponent.crownSelectedElite.healthBoostCoefficient - 1f) * 10f));
                        memberMaster.inventory.GiveItem(RoR2Content.Items.BoostDamage, Mathf.RoundToInt((tpComponent.crownSelectedElite.damageBoostCoefficient - 1f) * 10f));
                    }
                }
            }
        }

        private void BossGroup_DropRewards(On.RoR2.BossGroup.orig_DropRewards orig, BossGroup self)
        {
            var tpComponent = self.GetComponent<ExtraChallengeShrinesTeleporterComponent>();
            if (tpComponent)
            {
                if (tpComponent.rockShrineStacks > 0)
                {
                    self.dropTable = ExtraChallengeShrinesTeleporterComponent.rockShrineDropTable;
                }
            }
            orig(self);
            if (tpComponent)
            {
                if (tpComponent.crownShrineStacks > 0 && Run.instance && self.rng != null && self.dropPosition)
                {
                    var participatingPlayerCount = Run.instance.participatingPlayerCount;
                    if (participatingPlayerCount != 0)
                    {
                        var pickupIndex = self.rng.NextElementUniform(Run.instance.availableTier3DropList);
                        int crownDrops = Interactables.ShrineCrown.redDrops + Interactables.ShrineCrown.redDropsPerStack * (tpComponent.crownShrineStacks - 1);
                        if (self.scaleRewardsByPlayerCount) crownDrops *= participatingPlayerCount;
                        var dropVelocity = Quaternion.AngleAxis((float)UnityEngine.Random.Range(0, 360), Vector3.up) * (Vector3.up * 40f + Vector3.forward * 2.5f);
                        var extraRotationPerDrop = Quaternion.AngleAxis(360f / (float)crownDrops, Vector3.up);
                        for (var i = 0; i < crownDrops; i++)
                        {
                            PickupDropletController.CreatePickupDroplet(pickupIndex, self.dropPosition.position, dropVelocity);
                            dropVelocity = extraRotationPerDrop * dropVelocity;
                        }
                    }
                }
            }
        }

        private void CombatDirector_SetNextSpawnAsBoss(On.RoR2.CombatDirector.orig_SetNextSpawnAsBoss orig, CombatDirector self)
        {
            orig(self);

            var tpComponent = self.GetComponent<ExtraChallengeShrinesTeleporterComponent>();

            DirectorCard GetEyeShrineDirectorCard()
            {
                for (var i = 0; i < self.finalMonsterCardsSelection.Count; i++)
                {
                    var weightedSelectionChoice = self.finalMonsterCardsSelection.GetChoice(i);
                    var spawnCard = weightedSelectionChoice.value.spawnCard;
                    var bodyIndex = spawnCard.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>().bodyIndex;
                    if (bodyIndex == tpComponent.eyeSelectedBody)
                    {
                        return weightedSelectionChoice.value;
                    }
                }
                return null;
            }
            void PreventCheapSpawnSkippingBandaid()
            {
                var skipSpawnIfTooCheap = self.skipSpawnIfTooCheap;
                self.skipSpawnIfTooCheap = false;
                RoR2Application.fixedTimeTimers.CreateTimer(1f, () =>
                {
                    if (self)
                    {
                        self.skipSpawnIfTooCheap = skipSpawnIfTooCheap;
                    }
                });
            }

            if (tpComponent && tpComponent.teleporterInteraction.bossDirector == self)
            {
                if (tpComponent.rockShrineStacks > 0)
                {
                    var hordeMonsterCards = new WeightedSelection<DirectorCard>();
                    var hordeMonsterCardsBackup = new WeightedSelection<DirectorCard>();
                    for (var i = 0; i < self.finalMonsterCardsSelection.Count; i++)
                    {
                        var weightedSelectionChoice = self.finalMonsterCardsSelection.GetChoice(i);
                        var spawnCard = weightedSelectionChoice.value.spawnCard;
                        var isChampion = spawnCard.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>().isChampion;
                        if (!isChampion && weightedSelectionChoice.value.IsAvailable())
                        {
                            hordeMonsterCards.AddChoice(weightedSelectionChoice);
                            hordeMonsterCardsBackup.AddChoice(weightedSelectionChoice);
                        }
                    }
                    if (hordeMonsterCards.Count > 0)
                    {
                        // remove too low-cost horde enemies from the list
                        var minCreditsToSpend = self.monsterCredit;
                        var i = 0;
                        while (i < hordeMonsterCards.Count)
                        {
                            var testCard = hordeMonsterCards.GetChoice(i).value;
                            var highestEliteCostMultiplier = CombatDirector.CalcHighestEliteCostMultiplier(testCard.spawnCard.eliteRules);
                            if ((testCard.cost * highestEliteCostMultiplier * self.maximumNumberToSpawnBeforeSkipping) < minCreditsToSpend)
                            {
                                hordeMonsterCards.RemoveChoice(i);
                                continue;
                            }
                            i++;
                        }
                        if (hordeMonsterCards.Count == 0)
                        {
                            // all enemies are too low-cost! just spawn the strongest horde enemy!
                            var mostExpensiveCard = hordeMonsterCardsBackup.GetChoice(0);
                            for (i = 0; i < hordeMonsterCardsBackup.Count; i++)
                            {
                                var tempChoice = hordeMonsterCardsBackup.GetChoice(i);
                                if (tempChoice.value.cost > mostExpensiveCard.value.cost)
                                {
                                    mostExpensiveCard = tempChoice;
                                }
                            }
                            hordeMonsterCards.AddChoice(mostExpensiveCard);
                        }

                        var directorCard = hordeMonsterCards.Evaluate(tpComponent.rng.nextNormalizedFloat);
                        self.OverrideCurrentMonsterCard(directorCard);
                        PreventCheapSpawnSkippingBandaid();
                    }

                    // special case - both rock and eye are active
                    // we'll spawn the eye-selected body for each eye shrine stack as an extra boss in addition to the horde
                    if (tpComponent.eyeShrineStacks > 0 && tpComponent.eyeSelectedBody != BodyIndex.None)
                    {
                        var directorCard = GetEyeShrineDirectorCard();
                        if (directorCard != null)
                        {
                            // little delay to make it so the extra boss's name doesn't override the horde's name in the healthbar
                            RoR2Application.fixedTimeTimers.CreateTimer(0.5f, () =>
                            {
                                if (self)
                                {
                                    self.Spawn(
                                        directorCard.spawnCard,
                                        null,
                                        self.currentSpawnTarget ? self.currentSpawnTarget.transform : null,
                                        directorCard.spawnDistance,
                                        directorCard.preventOverhead,
                                        0f,
                                        DirectorPlacementRule.PlacementMode.Approximate
                                    );
                                }
                            });
                        }
                    }
                }
                else if (tpComponent.eyeShrineStacks > 0 && tpComponent.eyeSelectedBody != BodyIndex.None)
                {
                    var directorCard = GetEyeShrineDirectorCard();
                    if (directorCard != null)
                    {
                        self.OverrideCurrentMonsterCard(directorCard);
                        if (self.monsterCredit < directorCard.cost) self.monsterCredit = directorCard.cost;
                        PreventCheapSpawnSkippingBandaid();
                    }
                }
            }
        }
    }

    public class ExtraChallengeShrinesTeleporterComponent : MonoBehaviour
    {
        public TeleporterInteraction teleporterInteraction;
        public BossGroup bossGroup;
        public NetworkIdentity networkIdentity;

        public int crownShrineStacks = 0;
        public int rockShrineStacks = 0;
        public int eyeShrineStacks = 0;

        public GameObject crownShrineIndicator;
        public GameObject rockShrineIndicator;
        public GameObject eyeShrineIndicator;

        public Xoroshiro128Plus rng;
        public EliteDef crownSelectedElite;
        public BodyIndex eyeSelectedBody = BodyIndex.None;

        public void Awake()
        {
            teleporterInteraction = GetComponent<TeleporterInteraction>();
            bossGroup = GetComponent<BossGroup>();
            networkIdentity = GetComponent<NetworkIdentity>();
        }

        public void Start()
        {
            rng = new Xoroshiro128Plus(Run.instance.stageRng);
        }

        public void FixedUpdate()
        {
            crownShrineIndicator.SetActive(crownShrineStacks > 0 && !teleporterInteraction.isCharged);
            rockShrineIndicator.SetActive(rockShrineStacks > 0 && !teleporterInteraction.isCharged);
            eyeShrineIndicator.SetActive(eyeShrineStacks > 0 && !teleporterInteraction.isCharged);
        }

        public void ServerSendSyncShrineStacks()
        {
            if (!NetworkServer.active) return;
            new SyncShrineStacks(networkIdentity.netId, crownShrineStacks, rockShrineStacks, eyeShrineStacks).Send(NetworkDestination.Clients);
        }

        public void RollCrownElite()
        {
            var eliteTierSelection = new WeightedSelection<CombatDirector.EliteTierDef>();
            var stageClearCount = (float)(Run.instance ? Run.instance.stageClearCount : 0);
            for (var i = 1; i < CombatDirector.eliteTiers.Length; i++)
            {
                var eliteTier = CombatDirector.eliteTiers[i];
                if (eliteTier.CanSelect(SpawnCard.EliteRules.Default))
                {
                    // the further we go into a run, the more common higher-tier elites will be
                    eliteTierSelection.AddChoice(eliteTier, 1f / eliteTier.costMultiplier + 0.04f * eliteTier.costMultiplier * stageClearCount);
                }
            }
            if (eliteTierSelection.Count <= 0) eliteTierSelection.AddChoice(EliteAPI.VanillaFirstTierDef, 1f);
            
            if (eliteTierSelection.Count > 0)
            {
                var selectedTier = eliteTierSelection.Evaluate(rng.nextNormalizedFloat);
                crownSelectedElite = selectedTier.GetRandomAvailableEliteDef(rng);
            }
        }

        public class SyncShrineStacks : INetMessage
        {
            NetworkInstanceId objID;
            int crownShrineStacks;
            int rockShrineStacks;
            int eyeShrineStacks;

            public SyncShrineStacks()
            {
            }

            public SyncShrineStacks(NetworkInstanceId objID, int crownShrineStacks, int rockShrineStacks, int eyeShrineStacks)
            {
                this.objID = objID;
                this.crownShrineStacks = crownShrineStacks;
                this.rockShrineStacks = rockShrineStacks;
                this.eyeShrineStacks = eyeShrineStacks;
            }

            public void Deserialize(NetworkReader reader)
            {
                objID = reader.ReadNetworkId();
                crownShrineStacks = reader.ReadInt32();
                rockShrineStacks = reader.ReadInt32();
                eyeShrineStacks = reader.ReadInt32();
            }

            public void OnReceived()
            {
                if (NetworkServer.active) return;
                GameObject obj = Util.FindNetworkObject(objID);
                if (obj)
                {
                    ExtraChallengeShrinesTeleporterComponent tpComponent = obj.GetComponent<ExtraChallengeShrinesTeleporterComponent>();
                    if (tpComponent)
                    {
                        tpComponent.crownShrineStacks = crownShrineStacks;
                        tpComponent.rockShrineStacks = rockShrineStacks;
                        tpComponent.eyeShrineStacks = eyeShrineStacks;
                    }
                }
            }

            public void Serialize(NetworkWriter writer)
            {
                writer.Write(objID);
                writer.Write(crownShrineStacks);
                writer.Write(rockShrineStacks);
                writer.Write(eyeShrineStacks);
            }
        }

        public static Material crownShrineIndicatorMaterial;
        public static Material rockShrineIndicatorMaterial;
        public static Material eyeShrineIndicatorMaterial;
        public static PickupDropTable rockShrineDropTable;
    }

    public class ExtraChallengeShrinesContent : IContentPackProvider
    {
        public string identifier
        {
            get
            {
                return ExtraChallengeShrinesPlugin.PluginName;
            }
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            contentPack.identifier = identifier;
            MysticsRisky2Utils.ContentManagement.ContentLoadHelper contentLoadHelper = new MysticsRisky2Utils.ContentManagement.ContentLoadHelper();

            // Add content loading dispatchers to the content load helper
            System.Action[] loadDispatchers = new System.Action[]
            {
                () => contentLoadHelper.DispatchLoad<ItemDef>(ExtraChallengeShrinesPlugin.executingAssembly, typeof(MysticsRisky2Utils.BaseAssetTypes.BaseItem), x => contentPack.itemDefs.Add(x)),
                () => contentLoadHelper.DispatchLoad<EquipmentDef>(ExtraChallengeShrinesPlugin.executingAssembly, typeof(MysticsRisky2Utils.BaseAssetTypes.BaseEquipment), x => contentPack.equipmentDefs.Add(x)),
                () => contentLoadHelper.DispatchLoad<BuffDef>(ExtraChallengeShrinesPlugin.executingAssembly, typeof(MysticsRisky2Utils.BaseAssetTypes.BaseBuff), x => contentPack.buffDefs.Add(x)),
                () => contentLoadHelper.DispatchLoad<GameObject>(ExtraChallengeShrinesPlugin.executingAssembly, typeof(MysticsRisky2Utils.BaseAssetTypes.BaseInteractable), null),
                () => contentLoadHelper.DispatchLoad<GameObject>(ExtraChallengeShrinesPlugin.executingAssembly, typeof(MysticsRisky2Utils.BaseAssetTypes.BaseCharacterBody), x => contentPack.bodyPrefabs.Add(x)),
                () => contentLoadHelper.DispatchLoad<GameObject>(ExtraChallengeShrinesPlugin.executingAssembly, typeof(MysticsRisky2Utils.BaseAssetTypes.BaseCharacterMaster), x => contentPack.masterPrefabs.Add(x)),
                () => contentLoadHelper.DispatchLoad<object>(ExtraChallengeShrinesPlugin.executingAssembly, typeof(BlankLoadableAsset), null)
            };
            int num = 0;
            for (int i = 0; i < loadDispatchers.Length; i = num)
            {
                loadDispatchers[i]();
                args.ReportProgress(Util.Remap((float)(i + 1), 0f, (float)loadDispatchers.Length, 0f, 0.05f));
                yield return null;
                num = i + 1;
            }

            // Start loading content. Longest part of the loading process, so we will dedicate most of the progress bar to it
            while (contentLoadHelper.coroutine.MoveNext())
            {
                args.ReportProgress(Util.Remap(contentLoadHelper.progress.value, 0f, 1f, 0.05f, 0.9f));
                yield return contentLoadHelper.coroutine.Current;
            }

            // Populate static content pack fields and add various prefabs and scriptable objects generated during the content loading part to the content pack
            loadDispatchers = new System.Action[]
            {
                () => ContentLoadHelper.PopulateTypeFields<ItemDef>(typeof(Items), contentPack.itemDefs),
                () => ContentLoadHelper.PopulateTypeFields<EquipmentDef>(typeof(Equipment), contentPack.equipmentDefs),
                () => ContentLoadHelper.PopulateTypeFields<BuffDef>(typeof(Buffs), contentPack.buffDefs),
                () => contentPack.bodyPrefabs.Add(Resources.bodyPrefabs.ToArray()),
                () => contentPack.masterPrefabs.Add(Resources.masterPrefabs.ToArray()),
                () => contentPack.projectilePrefabs.Add(Resources.projectilePrefabs.ToArray()),
                () => contentPack.effectDefs.Add(Resources.effectPrefabs.ConvertAll(x => new EffectDef(x)).ToArray()),
                () => contentPack.networkSoundEventDefs.Add(Resources.networkSoundEventDefs.ToArray()),
                () => contentPack.unlockableDefs.Add(Resources.unlockableDefs.ToArray()),
                () => contentPack.entityStateTypes.Add(Resources.entityStateTypes.ToArray()),
                () => contentPack.entityStateConfigurations.Add(Resources.entityStateConfigurations.ToArray()),
                () => contentPack.skillDefs.Add(Resources.skillDefs.ToArray()),
                () => contentPack.skillFamilies.Add(Resources.skillFamilies.ToArray()),
                () => contentPack.networkedObjectPrefabs.Add(Resources.networkedObjectPrefabs.ToArray())
            };
            for (int i = 0; i < loadDispatchers.Length; i = num)
            {
                loadDispatchers[i]();
                args.ReportProgress(Util.Remap((float)(i + 1), 0f, (float)loadDispatchers.Length, 0.9f, 0.95f));
                yield return null;
                num = i + 1;
            }

            // Call "AfterContentPackLoaded" methods
            loadDispatchers = new System.Action[]
            {
                () => MysticsRisky2Utils.ContentManagement.ContentLoadHelper.InvokeAfterContentPackLoaded<MysticsRisky2Utils.BaseAssetTypes.BaseItem>(ExtraChallengeShrinesPlugin.executingAssembly),
                () => MysticsRisky2Utils.ContentManagement.ContentLoadHelper.InvokeAfterContentPackLoaded<MysticsRisky2Utils.BaseAssetTypes.BaseEquipment>(ExtraChallengeShrinesPlugin.executingAssembly),
                () => MysticsRisky2Utils.ContentManagement.ContentLoadHelper.InvokeAfterContentPackLoaded<MysticsRisky2Utils.BaseAssetTypes.BaseBuff>(ExtraChallengeShrinesPlugin.executingAssembly),
                () => MysticsRisky2Utils.ContentManagement.ContentLoadHelper.InvokeAfterContentPackLoaded<MysticsRisky2Utils.BaseAssetTypes.BaseInteractable>(ExtraChallengeShrinesPlugin.executingAssembly),
                () => MysticsRisky2Utils.ContentManagement.ContentLoadHelper.InvokeAfterContentPackLoaded<MysticsRisky2Utils.BaseAssetTypes.BaseCharacterBody>(ExtraChallengeShrinesPlugin.executingAssembly),
                () => MysticsRisky2Utils.ContentManagement.ContentLoadHelper.InvokeAfterContentPackLoaded<MysticsRisky2Utils.BaseAssetTypes.BaseCharacterMaster>(ExtraChallengeShrinesPlugin.executingAssembly)
            };
            for (int i = 0; i < loadDispatchers.Length; i = num)
            {
                loadDispatchers[i]();
                args.ReportProgress(Util.Remap((float)(i + 1), 0f, (float)loadDispatchers.Length, 0.95f, 0.99f));
                yield return null;
                num = i + 1;
            }

            loadDispatchers = null;
            yield break;
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
        {
            ContentPack.Copy(contentPack, args.output);
            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            args.ReportProgress(1f);
            yield break;
        }

        private ContentPack contentPack = new ContentPack();

        public static class Resources
        {
            public static List<GameObject> bodyPrefabs = new List<GameObject>();
            public static List<GameObject> masterPrefabs = new List<GameObject>();
            public static List<GameObject> projectilePrefabs = new List<GameObject>();
            public static List<GameObject> effectPrefabs = new List<GameObject>();
            public static List<NetworkSoundEventDef> networkSoundEventDefs = new List<NetworkSoundEventDef>();
            public static List<UnlockableDef> unlockableDefs = new List<UnlockableDef>();
            public static List<System.Type> entityStateTypes = new List<System.Type>();
            public static List<EntityStateConfiguration> entityStateConfigurations = new List<EntityStateConfiguration>();
            public static List<RoR2.Skills.SkillDef> skillDefs = new List<RoR2.Skills.SkillDef>();
            public static List<RoR2.Skills.SkillFamily> skillFamilies = new List<RoR2.Skills.SkillFamily>();
            public static List<GameObject> networkedObjectPrefabs = new List<GameObject>();
        }

        public static class Items
        {

        }

        public static class Equipment
        {

        }

        public static class Buffs
        {
            public static BuffDef MysticsBadItems_RhythmCombo;
        }
    }
}
