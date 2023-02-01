using UnityEngine;
using MysticsRisky2Utils;
using RoR2;
using RoR2.Skills;
using UnityEngine.Networking;
using MysticsRisky2Utils.BaseAssetTypes;
using MysticsRisky2Utils.ContentManagement;
using R2API;
using RoR2.Projectile;
using RoR2.Navigation;
using RoR2.Networking;
using UnityEngine.Events;

namespace ExtraChallengeShrines.Interactables
{
    public class ShrineCrown : BlankLoadableAsset
    {
        public static GameObject shrinePrefab;
        public static InteractableSpawnCard spawnCard;

        public static ConfigOptions.ConfigurableValue<float> bossCreditsPerStack = ConfigOptions.ConfigurableValue.CreateFloat(
            ExtraChallengeShrinesPlugin.PluginGUID,
            ExtraChallengeShrinesPlugin.PluginName,
            ExtraChallengeShrinesPlugin.config,
            "Shrine of the Sky",
            "Boss Credits Per Stack",
            600f,
            0f,
            100000f,
            "How many director credits to add for each time this shrine is used more than once?",
            useDefaultValueConfigEntry: ExtraChallengeShrinesPlugin.ignoreBalanceChanges
        );
        public static ConfigOptions.ConfigurableValue<int> redDrops = ConfigOptions.ConfigurableValue.CreateInt(
            ExtraChallengeShrinesPlugin.PluginGUID,
            ExtraChallengeShrinesPlugin.PluginName,
            ExtraChallengeShrinesPlugin.config,
            "Shrine of the Sky",
            "Red Drops",
            1,
            0,
            100,
            "How many extra red items to drop for completing the TP event?",
            useDefaultValueConfigEntry: ExtraChallengeShrinesPlugin.ignoreBalanceChanges
        );
        public static ConfigOptions.ConfigurableValue<int> redDropsPerStack = ConfigOptions.ConfigurableValue.CreateInt(
            ExtraChallengeShrinesPlugin.PluginGUID,
            ExtraChallengeShrinesPlugin.PluginName,
            ExtraChallengeShrinesPlugin.config,
            "Shrine of the Sky",
            "Red Drops Per Stack",
            1,
            0,
            100,
            "How many extra red items to drop for completing the TP event for each additional use of this shrine?",
            useDefaultValueConfigEntry: ExtraChallengeShrinesPlugin.ignoreBalanceChanges
        );

        public override void OnPluginAwake()
        {
            base.OnPluginAwake();
        }

        public override void OnLoad()
        {
            base.OnLoad();

            shrinePrefab = ExtraChallengeShrinesPlugin.AssetBundle.LoadAsset<GameObject>("Assets/Mods/ExtraChallengeShrines/ShrineCrown/ShrineCrown.prefab");

            foreach (Renderer renderer in shrinePrefab.GetComponentsInChildren<Renderer>())
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader.name == "Standard" && material.shader != HopooShaderToMaterial.Standard.shader)
                    {
                        HopooShaderToMaterial.Standard.Apply(material);
                        HopooShaderToMaterial.Standard.Dither(material);
                    }
                }
            }

            var existingSymbol = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/Shrines/ShrineBoss").transform.Find("Symbol").gameObject;
            var symbol = shrinePrefab.transform.Find("Symbol").gameObject;
            symbol.GetComponent<MeshFilter>().mesh = Object.Instantiate(existingSymbol.GetComponent<MeshFilter>().mesh);
            Material symbolMaterial = Object.Instantiate(existingSymbol.GetComponent<MeshRenderer>().material);
            symbol.GetComponent<MeshRenderer>().material = symbolMaterial;
            symbolMaterial.SetTexture("_MainTex", ExtraChallengeShrinesPlugin.AssetBundle.LoadAsset<Texture>("Assets/Mods/ExtraChallengeShrines/ShrineCrown/Symbol.png"));
            symbolMaterial.SetColor("_TintColor", new Color32(255, 57, 35, 255));

            ExtraChallengeShrinesTeleporterComponent.crownShrineIndicatorMaterial = symbolMaterial;

            var shrineBehaviour = shrinePrefab.AddComponent<ExtraChallengeShrinesShrineCrownBehaviour>();
            shrineBehaviour.symbolTransform = symbol.transform;

            ExtraChallengeShrinesContent.Resources.networkedObjectPrefabs.Add(shrinePrefab);

            spawnCard = ScriptableObject.CreateInstance<InteractableSpawnCard>();
            spawnCard.name = "iscExtraChallengeShrines_ShrineCrown";
            spawnCard.prefab = shrinePrefab;
            spawnCard.sendOverNetwork = true;
            spawnCard.hullSize = HullClassification.Golem;
            spawnCard.nodeGraphType = MapNodeGroup.GraphType.Ground;
            spawnCard.requiredFlags = NodeFlags.None;
            spawnCard.forbiddenFlags = NodeFlags.NoShrineSpawn;
            spawnCard.directorCreditCost = 20;
            spawnCard.occupyPosition = true;
            spawnCard.orientToFloor = true;
            spawnCard.slightlyRandomizeOrientation = true;
            spawnCard.skipSpawnWhenSacrificeArtifactEnabled = false;

            ConfigOptions.ConfigurableValue.CreateInt(
                ExtraChallengeShrinesPlugin.PluginGUID,
                ExtraChallengeShrinesPlugin.PluginName,
                ExtraChallengeShrinesPlugin.config,
                "Shrine of the Sky",
                "Director Credit Cost",
                20,
                0,
                1000,
                onChanged: (x) => spawnCard.directorCreditCost = x
            );

            var directorCardRare = new DirectorCard
            {
                spawnCard = spawnCard,
                selectionWeight = 1,
                spawnDistance = 0f,
                preventOverhead = false,
                minimumStageCompletions = 0,
                requiredUnlockableDef = null,
                forbiddenUnlockableDef = null
            };
            var directorCardCommon = new DirectorCard
            {
                spawnCard = spawnCard,
                selectionWeight = 10,
                spawnDistance = 0f,
                preventOverhead = false,
                minimumStageCompletions = 0,
                requiredUnlockableDef = null,
                forbiddenUnlockableDef = null
            };
            // base game
            BaseInteractable.AddDirectorCardTo("blackbeach", "Shrines", directorCardRare);
            BaseInteractable.AddDirectorCardTo("foggyswamp", "Shrines", directorCardRare);
            BaseInteractable.AddDirectorCardTo("frozenwall", "Shrines", directorCardCommon);
            BaseInteractable.AddDirectorCardTo("golemplains", "Shrines", directorCardRare);
            BaseInteractable.AddDirectorCardTo("goolake", "Shrines", directorCardRare);
            BaseInteractable.AddDirectorCardTo("shipgraveyard", "Shrines", directorCardCommon);
            BaseInteractable.AddDirectorCardTo("skymeadow", "Shrines", directorCardCommon);
            BaseInteractable.AddDirectorCardTo("wispgraveyard", "Shrines", directorCardRare);
            // sotv
            BaseInteractable.AddDirectorCardTo("ancientloft", "Shrines", directorCardRare);
            BaseInteractable.AddDirectorCardTo("snowyforest", "Shrines", directorCardRare);
            BaseInteractable.AddDirectorCardTo("sulfurpools", "Shrines", directorCardRare);
        }
    }

    public class ExtraChallengeShrinesShrineCrownBehaviour : NetworkBehaviour
    {
        public int maxPurchaseCount = 1;
        public float costMultiplierPerPurchase = 2f;
        public Transform symbolTransform;
        public PurchaseInteraction purchaseInteraction;
        public int purchaseCount;
        public float refreshTimer;
        public const float refreshDuration = 0.5f;
        public bool waitingForRefresh;

        public void Start()
        {
            purchaseInteraction = GetComponent<PurchaseInteraction>();
            purchaseInteraction.onPurchase.AddListener((interactor) =>
            {
                purchaseInteraction.SetAvailable(false);
                AddShrineStack(interactor);
            });
        }

        public void FixedUpdate()
        {
            if (waitingForRefresh)
            {
                refreshTimer -= Time.fixedDeltaTime;
                if (refreshTimer <= 0f && purchaseCount < maxPurchaseCount)
                {
                    purchaseInteraction.SetAvailable(true);
                    purchaseInteraction.Networkcost = (int)(100f * (1f - Mathf.Pow(1f - (float)purchaseInteraction.cost / 100f, costMultiplierPerPurchase)));
                    waitingForRefresh = false;
                }
            }
        }

        [Server]
        public void AddShrineStack(Interactor interactor)
        {
            waitingForRefresh = true;
            if (TeleporterInteraction.instance && TeleporterInteraction.instance.activationState <= TeleporterInteraction.ActivationState.IdleToCharging)
            {
                var tpComponent = TeleporterInteraction.instance.GetComponent<ExtraChallengeShrinesTeleporterComponent>();
                if (!tpComponent) return;

                tpComponent.crownShrineStacks++;
                tpComponent.ServerSendSyncShrineStacks();
            }
            CharacterBody component = interactor.GetComponent<CharacterBody>();
            Chat.SendBroadcastChat(new Chat.SubjectFormatChatMessage
            {
                subjectAsCharacterBody = component,
                baseToken = "EXTRACHALLENGESHRINES_SHRINE_CROWN_USE_MESSAGE"
            });
            EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ShrineUseEffect"), new EffectData
            {
                origin = transform.position,
                rotation = Quaternion.identity,
                scale = 1f,
                color = new Color(0.7372549f, 0.905882359f, 0.945098042f)
            }, true);
            purchaseCount++;
            refreshTimer = 2f;
            if (purchaseCount >= maxPurchaseCount)
            {
                symbolTransform.gameObject.SetActive(false);
            }
        }

        public override int GetNetworkChannel()
        {
            return QosChannelIndex.defaultReliable.intVal;
        }
    }
}
