using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UdonSharpEditor;
using UdonSharp;

namespace M2922.Editor
{
    /// <summary>
    /// Outil de setup automatique pour le système M2922 PvP
    /// Crée la hiérarchie de GameObjects et configure les références automatiquement
    /// Sauvegarde les configurations entre les sessions avec EditorPrefs
    /// </summary>
    public class M2922_ProjectSetup : EditorWindow
    {
        // === EDITORPREFS KEYS ===
        private const string PREF_PREFIX = "M2922_Setup_";
        private const string PREF_SETUP_CORE = PREF_PREFIX + "SetupCore";
        private const string PREF_SETUP_EVENTBUS = PREF_PREFIX + "SetupEventBus";
        private const string PREF_SETUP_TEAMS = PREF_PREFIX + "SetupTeams";
        private const string PREF_SETUP_COMBAT = PREF_PREFIX + "SetupCombat";
        private const string PREF_SETUP_SPAWNING = PREF_PREFIX + "SetupSpawning";
        private const string PREF_SETUP_SCORING = PREF_PREFIX + "SetupScoring";
        private const string PREF_SETUP_VEHICLES = PREF_PREFIX + "SetupVehicles";
        private const string PREF_SETUP_GAMEMODES = PREF_PREFIX + "SetupGameModes";
        private const string PREF_TEAM_COUNT = PREF_PREFIX + "TeamCount";
        private const string PREF_SPAWN_POINTS = PREF_PREFIX + "SpawnPointsPerTeam";
        private const string PREF_SPAWN_STRATEGY = PREF_PREFIX + "SpawnStrategy";
        private const string PREF_SPAWN_PROTECTION = PREF_PREFIX + "SpawnProtection";
        private const string PREF_SPAWN_COOLDOWN = PREF_PREFIX + "SpawnCooldown";
        private const string PREF_MIN_ENEMY_DIST = PREF_PREFIX + "MinEnemyDistance";
        private const string PREF_AUTO_DISCOVER = PREF_PREFIX + "AutoDiscoverSpawns";
        private const string PREF_AUTO_RESPAWN = PREF_PREFIX + "AutoRespawn";
        private const string PREF_RESPAWN_DELAY = PREF_PREFIX + "RespawnDelay";
        private const string PREF_AUTO_BALANCE = PREF_PREFIX + "AutoBalance";
        private const string PREF_FRIENDLY_FIRE = PREF_PREFIX + "FriendlyFire";
        private const string PREF_FF_DAMAGE_MULT = PREF_PREFIX + "FFDamageMultiplier";
        private const string PREF_DEBUG_MODE = PREF_PREFIX + "DebugMode";
        private const string PREF_EVENT_POOL_SIZE = PREF_PREFIX + "EventPoolSize";
        private const string PREF_SETUP_PLAYER = PREF_PREFIX + "SetupPlayer";
        private const string PREF_PLAYER_MAX_HEALTH = PREF_PREFIX + "PlayerMaxHealth";
        private const string PREF_PLAYER_REGEN = PREF_PREFIX + "PlayerRegen";
        private const string PREF_PLAYER_REGEN_RATE = PREF_PREFIX + "PlayerRegenRate";
        private const string PREF_PLAYER_REGEN_DELAY = PREF_PREFIX + "PlayerRegenDelay";
        private const string PREF_PLAYER_HEADSHOT_MULT = PREF_PREFIX + "PlayerHeadshotMult";
        private const string PREF_PLAYER_RESPAWN_DELAY = PREF_PREFIX + "PlayerRespawnDelay";
        private const string PREF_PLAYER_DISABLE_ON_DEATH = PREF_PREFIX + "PlayerDisableOnDeath";
        
        // === CORE SYSTEMS ===
        private bool setupCore = true;
        private bool setupEventBus = true;
        private int eventPoolSize = 8;
        
        // === PLAYER OBJECT ===
        private bool setupPlayer = true;
        private float playerMaxHealth = 100f;
        private bool playerRegen = false;
        private float playerRegenRate = 5f;
        private float playerRegenDelay = 3f;
        private float playerHeadshotMultiplier = 2f;
        private float playerAutoRespawnDelay = 5f;
        private bool playerDisableOnDeath = true;
        
        // === GAME MODULES ===
        private bool setupTeams = true;
        private bool setupCombat = true;
        private bool setupSpawning = true;
        private bool setupScoring = false;
        private bool setupVehicles = false;
        private bool setupGameModes = false;
        
        // === TEAM SETTINGS ===
        private int teamCount = 4;
        private int spawnPointsPerTeam = 4;
        private bool autoBalance = true;
        
        // === SPAWN SETTINGS ===
        private int spawnStrategy = 0; // 0=Random, 1=Sequential, 2=LeastRecent, 3=Farthest
        private float spawnProtectionDuration = 3f;
        private float spawnCooldown = 2f;
        private float minEnemyDistance = 5f;
        private bool autoDiscoverSpawns = true;
        private bool autoRespawn = true;
        private float respawnDelay = 5f;
        private bool allowFriendlyFire = false;
        private float friendlyFireDamageMultiplier = 0.5f; // 50% par défaut
        private bool debugMode = true;
        
        // Noms et couleurs personnalisés (initialisés avec les defaults)
        private string[] customTeamNames;
        private Color[] customTeamColors;
        private bool showTeamEditor = false;
        
        // Noms et couleurs par défaut (identiques à TeamManager)
        private static readonly string[] DefaultTeamNames = new string[]
        {
            "Red", "Blue", "Green", "Yellow", "Purple", "Orange", "Pink", "Cyan",
            "Lime", "Magenta", "Teal", "Navy", "Maroon", "Olive", "Silver", "Gold"
        };
        
        private static readonly Color[] DefaultTeamColors = new Color[]
        {
            new Color(1f, 0.2f, 0.2f),      // Red
            new Color(0.2f, 0.4f, 1f),      // Blue
            new Color(0.2f, 0.8f, 0.2f),    // Green
            new Color(1f, 0.9f, 0.2f),      // Yellow
            new Color(0.6f, 0.2f, 0.8f),    // Purple
            new Color(1f, 0.6f, 0.2f),      // Orange
            new Color(1f, 0.4f, 0.7f),      // Pink
            new Color(0.2f, 0.8f, 0.8f),    // Cyan
            new Color(0.6f, 1f, 0.2f),      // Lime
            new Color(0.8f, 0.2f, 0.6f),    // Magenta
            new Color(0.2f, 0.6f, 0.6f),    // Teal
            new Color(0.1f, 0.1f, 0.5f),    // Navy
            new Color(0.5f, 0.1f, 0.1f),    // Maroon
            new Color(0.5f, 0.5f, 0.1f),    // Olive
            new Color(0.75f, 0.75f, 0.75f), // Silver
            new Color(1f, 0.84f, 0f)        // Gold
        };
        
        // === SCENE REFERENCES ===
        private GameObject rootObject;
        private GameObject managerObj;
        private GameObject eventBusObj;
        private GameObject teamManagerObj;
        
        private Vector2 scrollPosition;
        
        [MenuItem("M2922/Setup/Project Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<M2922_ProjectSetup>("M2922 Setup Wizard");
            window.minSize = new Vector2(500, 700);
            window.Show();
        }
        
        private void OnEnable()
        {
            LoadPreferences();
            InitializeTeamArrays();
        }
        
        private void InitializeTeamArrays()
        {
            // Initialiser les tableaux si nécessaire
            if (customTeamNames == null || customTeamNames.Length != 16)
            {
                customTeamNames = new string[16];
                for (int i = 0; i < 16; i++)
                {
                    customTeamNames[i] = DefaultTeamNames[i];
                }
            }
            
            if (customTeamColors == null || customTeamColors.Length != 16)
            {
                customTeamColors = new Color[16];
                for (int i = 0; i < 16; i++)
                {
                    customTeamColors[i] = DefaultTeamColors[i];
                }
            }
        }
        
        private void LoadPreferences()
        {
            setupCore = EditorPrefs.GetBool(PREF_SETUP_CORE, true);
            setupEventBus = EditorPrefs.GetBool(PREF_SETUP_EVENTBUS, true);
            setupTeams = EditorPrefs.GetBool(PREF_SETUP_TEAMS, true);
            setupCombat = EditorPrefs.GetBool(PREF_SETUP_COMBAT, true);
            setupSpawning = EditorPrefs.GetBool(PREF_SETUP_SPAWNING, true);
            setupScoring = EditorPrefs.GetBool(PREF_SETUP_SCORING, false);
            setupVehicles = EditorPrefs.GetBool(PREF_SETUP_VEHICLES, false);
            setupGameModes = EditorPrefs.GetBool(PREF_SETUP_GAMEMODES, false);
            
            setupPlayer = EditorPrefs.GetBool(PREF_SETUP_PLAYER, true);
            playerMaxHealth = EditorPrefs.GetFloat(PREF_PLAYER_MAX_HEALTH, 100f);
            playerRegen = EditorPrefs.GetBool(PREF_PLAYER_REGEN, false);
            playerRegenRate = EditorPrefs.GetFloat(PREF_PLAYER_REGEN_RATE, 5f);
            playerRegenDelay = EditorPrefs.GetFloat(PREF_PLAYER_REGEN_DELAY, 3f);
            playerHeadshotMultiplier = EditorPrefs.GetFloat(PREF_PLAYER_HEADSHOT_MULT, 2f);
            playerAutoRespawnDelay = EditorPrefs.GetFloat(PREF_PLAYER_RESPAWN_DELAY, 5f);
            playerDisableOnDeath = EditorPrefs.GetBool(PREF_PLAYER_DISABLE_ON_DEATH, true);
            
            teamCount = EditorPrefs.GetInt(PREF_TEAM_COUNT, 4);
            spawnPointsPerTeam = EditorPrefs.GetInt(PREF_SPAWN_POINTS, 4);
            autoBalance = EditorPrefs.GetBool(PREF_AUTO_BALANCE, true);
            
            spawnStrategy = EditorPrefs.GetInt(PREF_SPAWN_STRATEGY, 0);
            spawnProtectionDuration = EditorPrefs.GetFloat(PREF_SPAWN_PROTECTION, 3f);
            spawnCooldown = EditorPrefs.GetFloat(PREF_SPAWN_COOLDOWN, 2f);
            minEnemyDistance = EditorPrefs.GetFloat(PREF_MIN_ENEMY_DIST, 5f);
            autoDiscoverSpawns = EditorPrefs.GetBool(PREF_AUTO_DISCOVER, true);
            autoRespawn = EditorPrefs.GetBool(PREF_AUTO_RESPAWN, true);
            respawnDelay = EditorPrefs.GetFloat(PREF_RESPAWN_DELAY, 5f);
            allowFriendlyFire = EditorPrefs.GetBool(PREF_FRIENDLY_FIRE, false);
            friendlyFireDamageMultiplier = EditorPrefs.GetFloat(PREF_FF_DAMAGE_MULT, 0.5f);
            debugMode = EditorPrefs.GetBool(PREF_DEBUG_MODE, true);
            eventPoolSize = EditorPrefs.GetInt(PREF_EVENT_POOL_SIZE, 8);
        }
        
        private void SavePreferences()
        {
            EditorPrefs.SetBool(PREF_SETUP_CORE, setupCore);
            EditorPrefs.SetBool(PREF_SETUP_EVENTBUS, setupEventBus);
            EditorPrefs.SetBool(PREF_SETUP_TEAMS, setupTeams);
            EditorPrefs.SetBool(PREF_SETUP_COMBAT, setupCombat);
            EditorPrefs.SetBool(PREF_SETUP_SPAWNING, setupSpawning);
            EditorPrefs.SetBool(PREF_SETUP_SCORING, setupScoring);
            EditorPrefs.SetBool(PREF_SETUP_VEHICLES, setupVehicles);
            EditorPrefs.SetBool(PREF_SETUP_GAMEMODES, setupGameModes);
            
            EditorPrefs.SetBool(PREF_SETUP_PLAYER, setupPlayer);
            EditorPrefs.SetFloat(PREF_PLAYER_MAX_HEALTH, playerMaxHealth);
            EditorPrefs.SetBool(PREF_PLAYER_REGEN, playerRegen);
            EditorPrefs.SetFloat(PREF_PLAYER_REGEN_RATE, playerRegenRate);
            EditorPrefs.SetFloat(PREF_PLAYER_REGEN_DELAY, playerRegenDelay);
            EditorPrefs.SetFloat(PREF_PLAYER_HEADSHOT_MULT, playerHeadshotMultiplier);
            EditorPrefs.SetFloat(PREF_PLAYER_RESPAWN_DELAY, playerAutoRespawnDelay);
            EditorPrefs.SetBool(PREF_PLAYER_DISABLE_ON_DEATH, playerDisableOnDeath);
            
            EditorPrefs.SetInt(PREF_TEAM_COUNT, teamCount);
            EditorPrefs.SetInt(PREF_SPAWN_POINTS, spawnPointsPerTeam);
            EditorPrefs.SetBool(PREF_AUTO_BALANCE, autoBalance);
            
            EditorPrefs.SetInt(PREF_SPAWN_STRATEGY, spawnStrategy);
            EditorPrefs.SetFloat(PREF_SPAWN_PROTECTION, spawnProtectionDuration);
            EditorPrefs.SetFloat(PREF_SPAWN_COOLDOWN, spawnCooldown);
            EditorPrefs.SetFloat(PREF_MIN_ENEMY_DIST, minEnemyDistance);
            EditorPrefs.SetBool(PREF_AUTO_DISCOVER, autoDiscoverSpawns);
            EditorPrefs.SetBool(PREF_AUTO_RESPAWN, autoRespawn);
            EditorPrefs.SetFloat(PREF_RESPAWN_DELAY, respawnDelay);
            EditorPrefs.SetBool(PREF_FRIENDLY_FIRE, allowFriendlyFire);
            EditorPrefs.SetFloat(PREF_FF_DAMAGE_MULT, friendlyFireDamageMultiplier);
            EditorPrefs.SetBool(PREF_DEBUG_MODE, debugMode);
            EditorPrefs.SetInt(PREF_EVENT_POOL_SIZE, eventPoolSize);
            
            Debug.Log("[M2922 Setup] Configuration sauvegardée");
        }
        
        private void ResetPreferences()
        {
            if (EditorUtility.DisplayDialog("Reset Configuration",
                "Réinitialiser tous les paramètres aux valeurs par défaut ?",
                "Reset", "Annuler"))
            {
                EditorPrefs.DeleteKey(PREF_SETUP_CORE);
                EditorPrefs.DeleteKey(PREF_SETUP_EVENTBUS);
                EditorPrefs.DeleteKey(PREF_SETUP_TEAMS);
                EditorPrefs.DeleteKey(PREF_SETUP_COMBAT);
                EditorPrefs.DeleteKey(PREF_SETUP_SPAWNING);
                EditorPrefs.DeleteKey(PREF_SETUP_SCORING);
                EditorPrefs.DeleteKey(PREF_SETUP_VEHICLES);
                EditorPrefs.DeleteKey(PREF_SETUP_GAMEMODES);
                EditorPrefs.DeleteKey(PREF_TEAM_COUNT);
                EditorPrefs.DeleteKey(PREF_SPAWN_POINTS);
                EditorPrefs.DeleteKey(PREF_AUTO_BALANCE);
                EditorPrefs.DeleteKey(PREF_FRIENDLY_FIRE);
                EditorPrefs.DeleteKey(PREF_FF_DAMAGE_MULT);
                EditorPrefs.DeleteKey(PREF_SPAWN_STRATEGY);
                EditorPrefs.DeleteKey(PREF_SPAWN_PROTECTION);
                EditorPrefs.DeleteKey(PREF_SPAWN_COOLDOWN);
                EditorPrefs.DeleteKey(PREF_MIN_ENEMY_DIST);
                EditorPrefs.DeleteKey(PREF_AUTO_DISCOVER);
                EditorPrefs.DeleteKey(PREF_AUTO_RESPAWN);
                EditorPrefs.DeleteKey(PREF_RESPAWN_DELAY);
                EditorPrefs.DeleteKey(PREF_DEBUG_MODE);
                EditorPrefs.DeleteKey(PREF_EVENT_POOL_SIZE);
                EditorPrefs.DeleteKey(PREF_SETUP_PLAYER);
                EditorPrefs.DeleteKey(PREF_PLAYER_MAX_HEALTH);
                EditorPrefs.DeleteKey(PREF_PLAYER_REGEN);
                EditorPrefs.DeleteKey(PREF_PLAYER_REGEN_RATE);
                EditorPrefs.DeleteKey(PREF_PLAYER_REGEN_DELAY);
                EditorPrefs.DeleteKey(PREF_PLAYER_HEADSHOT_MULT);
                EditorPrefs.DeleteKey(PREF_PLAYER_RESPAWN_DELAY);
                EditorPrefs.DeleteKey(PREF_PLAYER_DISABLE_ON_DEATH);
                
                LoadPreferences();
                Debug.Log("[M2922 Setup] Configuration réinitialisée");
            }
        }
        
        private string GetTeamPreviewText()
        {
            string preview = "";
            int previewCount = Mathf.Min(teamCount, 4);
            for (int i = 0; i < previewCount; i++)
            {
                string teamName = customTeamNames != null && i < customTeamNames.Length ? customTeamNames[i] : DefaultTeamNames[i];
                preview += $"  • {teamName}\n";
            }
            if (teamCount > previewCount)
            {
                preview += $"  ... et {teamCount - previewCount} autres";
            }
            return preview.TrimEnd('\n');
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("M2922 PvP System - Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Cet outil va créer automatiquement la hiérarchie de GameObjects nécessaire pour le système M2922.\n" +
                "Les références entre objets seront configurées automatiquement.\n\n" +
                "💾 La configuration est sauvegardée automatiquement et restaurée à l'ouverture.\n\n" +
                "📋 ARCHITECTURE À 3 NIVEAUX:\n" +
                "• NIVEAU 1 - Core + Combat: Sandbox PvP libre (SANS règles)\n" +
                "• NIVEAU 2 - Core + Combat + Teams: Sandbox team-based (SANS règles)\n" +
                "• NIVEAU 3 - Avec GameMode: Créez vos règles personnalisées dans GameModes/\n\n" +
                "⚡ Le core Manager ne contient AUCUNE règle de jeu.\n" +
                "Pour rounds/scores/timers, créez un script GameMode personnalisé!",
                MessageType.Info
            );
            
            GUILayout.Space(10);
            
            // === CONFIGURATION SUMMARY ===
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("📊 Résumé de Configuration", EditorStyles.boldLabel);
            
            // Résumé dynamique selon les modules activés
            if (setupCore)
            {
                EditorGUILayout.LabelField("✓ Core:", "Manager" + (setupEventBus ? $" + EventBus ({eventPoolSize} slots)" : ""));
            }
            
            if (setupTeams)
            {
                EditorGUILayout.LabelField("✓ Teams:", $"{teamCount} équipes, {spawnPointsPerTeam} spawns/team");
                EditorGUILayout.LabelField("  Options:", 
                    $"AutoBalance: {(autoBalance ? "✓" : "✗")}, " +
                    $"FF: {(allowFriendlyFire ? "✓" : "✗")}" +
                    (allowFriendlyFire ? $" ({Mathf.RoundToInt(friendlyFireDamageMultiplier * 100)}% dégâts)" : ""));
            }
            
            if (setupCombat)
            {
                EditorGUILayout.LabelField("✓ Combat:", "Système de dégâts");
            }
            
            if (setupPlayer)
            {
                EditorGUILayout.LabelField("✓ Player Object:", $"{playerMaxHealth} HP" + (playerRegen ? $", régén {playerRegenRate}/s" : "") + $", headshot x{playerHeadshotMultiplier}");
            }
            
            if (setupSpawning)
            {
                string spawnInfo = setupTeams 
                    ? $"{teamCount}x{spawnPointsPerTeam} spawns (par équipe)" 
                    : $"{spawnPointsPerTeam} spawns (FFA)";
                EditorGUILayout.LabelField("✓ Spawning:", spawnInfo);
            }
            
            if (setupScoring)
            {
                EditorGUILayout.LabelField("✓ Scoring:", "Système de score");
            }
            
            if (setupVehicles)
            {
                EditorGUILayout.LabelField("✓ Vehicles:", "Système de véhicules");
            }
            
            if (setupGameModes)
            {
                EditorGUILayout.LabelField("✓ GameModes:", "Dossier modes de jeu");
            }
            
            EditorGUILayout.LabelField("Debug:", debugMode ? "✓ Activé" : "✗ Désactivé");
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            
            // === CORE SYSTEMS ===
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("📦 Systèmes Core (requis)", EditorStyles.boldLabel);
            
            setupCore = EditorGUILayout.Toggle("Manager Principal", setupCore);
            GUI.enabled = setupCore;
            EditorGUI.indentLevel++;
            setupEventBus = EditorGUILayout.Toggle("EventBus", setupEventBus);
            EditorGUI.indentLevel++;
            GUI.enabled = setupCore && setupEventBus;
            eventPoolSize = EditorGUILayout.IntSlider("Pool EventData Slots", eventPoolSize, 4, 32);
            EditorGUILayout.HelpBox(
                "Nombre de slots dans le pool d'EventData (ring buffer).\n" +
                "8 suffisent pour la plupart des cas. Augmenter si plus de 8 événements réseau arrivent simultanément.",
                MessageType.Info
            );
            GUI.enabled = setupCore;
            EditorGUI.indentLevel--;
            EditorGUI.indentLevel--;
            GUI.enabled = true;
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            
            // === GAME MODULES ===
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("🎮 Modules de Jeu", EditorStyles.boldLabel);
            
            setupTeams = EditorGUILayout.Toggle("Système d'Équipes (Team-based)", setupTeams);
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "TeamManager est OPTIONNEL:\n" +
                "✓ Activé: Modes team-based (TDM, CTF, Domination)\n" +
                "✗ Désactivé: Mode FFA (Free-for-all/Chacun pour soi)",
                MessageType.Info
            );
            EditorGUI.indentLevel--;
            
            setupCombat = EditorGUILayout.Toggle("Système de Combat", setupCombat);
            setupSpawning = EditorGUILayout.Toggle("Système de Spawn", setupSpawning);
            setupPlayer = EditorGUILayout.Toggle("Player Object (VRC Player Object)", setupPlayer);
            setupScoring = EditorGUILayout.Toggle("Système de Score", setupScoring);
            setupVehicles = EditorGUILayout.Toggle("Système de Véhicules", setupVehicles);
            setupGameModes = EditorGUILayout.Toggle("Modes de Jeu", setupGameModes);
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            
            // === TEAM SETTINGS ===
            if (setupTeams)
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("⚔️ Configuration des Équipes", EditorStyles.boldLabel);
                
                teamCount = EditorGUILayout.IntSlider("Nombre d'équipes", teamCount, 2, 16);
                
                GUILayout.Space(5);
                autoBalance = EditorGUILayout.Toggle("Auto-balance des équipes", autoBalance);
                allowFriendlyFire = EditorGUILayout.Toggle("Friendly Fire autorisé", allowFriendlyFire);
                
                // Afficher le slider de dégâts FF seulement si FF activé
                if (allowFriendlyFire)
                {
                    EditorGUI.indentLevel++;
                    friendlyFireDamageMultiplier = EditorGUILayout.Slider(
                        new GUIContent(
                            "% Dégâts FF",
                            "Pourcentage de dégâts infligés aux alliés (0% = aucun dégât, 100% = dégâts complets)"
                        ),
                        friendlyFireDamageMultiplier,
                        0f,
                        1f
                    );
                    
                    EditorGUILayout.LabelField("", $"{Mathf.RoundToInt(friendlyFireDamageMultiplier * 100)}% des dégâts normaux", EditorStyles.miniLabel);
                    EditorGUI.indentLevel--;
                }
                
                GUILayout.Space(10);
                
                // === TEAM CUSTOMIZATION ===
                showTeamEditor = EditorGUILayout.Foldout(showTeamEditor, "🎨 Personnaliser Noms/Couleurs", true);
                
                if (showTeamEditor)
                {
                    EditorGUI.indentLevel++;
                    
                    EditorGUILayout.HelpBox(
                        "Personnalisez les noms et couleurs de vos équipes.\n" +
                        "Les valeurs par défaut seront utilisées si vous ne modifiez rien.",
                        MessageType.Info
                    );
                    
                    GUILayout.Space(5);
                    
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("🔄 Réinitialiser aux Défauts"))
                    {
                        for (int i = 0; i < teamCount; i++)
                        {
                            customTeamNames[i] = DefaultTeamNames[i];
                            customTeamColors[i] = DefaultTeamColors[i];
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    GUILayout.Space(5);
                    
                    // Afficher uniquement les équipes actives
                    for (int i = 0; i < teamCount; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"Équipe {i + 1}:", GUILayout.Width(70));
                        customTeamNames[i] = EditorGUILayout.TextField(customTeamNames[i], GUILayout.Width(120));
                        customTeamColors[i] = EditorGUILayout.ColorField(customTeamColors[i], GUILayout.Width(60));
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUI.indentLevel--;
                }
                else
                {
                    // Afficher un preview quand le foldout est fermé
                    EditorGUILayout.HelpBox(
                        $"Preview des {teamCount} premières équipes:\n" +
                        GetTeamPreviewText(),
                        MessageType.Info
                    );
                }
                
                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
            }
            
            // === SPAWN SETTINGS ===
            if (setupSpawning)
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("🎯 Système de Spawn", EditorStyles.boldLabel);
                
                if (setupTeams)
                {
                    EditorGUILayout.HelpBox(
                        "Mode Team-based: Les spawn points seront créés par équipe.",
                        MessageType.Info
                    );
                    
                    spawnPointsPerTeam = EditorGUILayout.IntSlider("Spawn Points par équipe", spawnPointsPerTeam, 1, 10);
                    EditorGUILayout.LabelField("", $"Total: {teamCount * spawnPointsPerTeam} spawns", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Mode FFA: Les spawn points seront créés en mode générique (pas d'équipes).",
                        MessageType.Info
                    );
                    
                    spawnPointsPerTeam = EditorGUILayout.IntSlider("Nombre de spawn points", spawnPointsPerTeam, 4, 20);
                }
                
                GUILayout.Space(10);
                
                // Stratégie de spawn
                string[] spawnStrategyNames = new string[] { "Random", "Sequential", "LeastRecent", "Farthest" };
                spawnStrategy = EditorGUILayout.Popup("Stratégie de Spawn", spawnStrategy, spawnStrategyNames);
                
                GUILayout.Space(5);
                
                // Paramètres de spawn
                spawnProtectionDuration = EditorGUILayout.Slider(
                    new GUIContent("Protection après Spawn (s)", "Durée d'invincibilité après spawn"),
                    spawnProtectionDuration, 0f, 10f
                );
                
                spawnCooldown = EditorGUILayout.Slider(
                    new GUIContent("Cooldown Spawn (s)", "Délai minimum entre deux spawns au même point"),
                    spawnCooldown, 0f, 10f
                );
                
                minEnemyDistance = EditorGUILayout.Slider(
                    new GUIContent("Distance Min Ennemis (m)", "Distance minimum entre spawn et ennemis (0 = désactivé)"),
                    minEnemyDistance, 0f, 20f
                );
                
                GUILayout.Space(10);
                
                // Auto-discovery et Respawn
                EditorGUILayout.LabelField("Options Auto", EditorStyles.boldLabel);
                autoDiscoverSpawns = EditorGUILayout.Toggle("Auto Discover Spawn Points", autoDiscoverSpawns);
                EditorGUILayout.HelpBox(
                    "Si activé, le SpawnManager trouvera automatiquement tous les M2922_SpawnPoint dans la scène.",
                    MessageType.Info
                );
                
                GUILayout.Space(5);
                
                autoRespawn = EditorGUILayout.Toggle("Auto Respawn", autoRespawn);
                if (autoRespawn)
                {
                    EditorGUI.indentLevel++;
                    respawnDelay = EditorGUILayout.Slider("Délai Respawn (s)", respawnDelay, 0f, 30f);
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
            }
            
            // === PLAYER OBJECT SETTINGS ===
            if (setupPlayer)
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("👤 Configuration du Player Object", EditorStyles.boldLabel);
                
                EditorGUILayout.HelpBox(
                    "Crée un VRC Player Object template avec M2922_HealthController + M2922_PlayerController.\n" +
                    "VRChat instancie automatiquement une copie de ce template pour chaque joueur.",
                    MessageType.Info
                );
                
                GUILayout.Space(5);
                GUILayout.Label("Santé", EditorStyles.boldLabel);
                playerMaxHealth = EditorGUILayout.FloatField("Max Health", playerMaxHealth);
                
                playerRegen = EditorGUILayout.Toggle("Régénération de santé", playerRegen);
                if (playerRegen)
                {
                    EditorGUI.indentLevel++;
                    playerRegenRate = EditorGUILayout.Slider("Vitesse régén (HP/s)", playerRegenRate, 0.5f, 50f);
                    playerRegenDelay = EditorGUILayout.Slider("Délai régén après dégât (s)", playerRegenDelay, 0f, 10f);
                    EditorGUI.indentLevel--;
                }
                
                GUILayout.Space(5);
                GUILayout.Label("Dégâts", EditorStyles.boldLabel);
                playerHeadshotMultiplier = EditorGUILayout.Slider("Multiplicateur Headshot", playerHeadshotMultiplier, 1f, 5f);
                
                GUILayout.Space(5);
                GUILayout.Label("Mort", EditorStyles.boldLabel);
                playerAutoRespawnDelay = EditorGUILayout.Slider("Délai Respawn Auto (s)", playerAutoRespawnDelay, 0f, 30f);
                playerDisableOnDeath = EditorGUILayout.Toggle("Désactiver à la mort", playerDisableOnDeath);
                
                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
            }
            
            // === DEBUG MODE ===
            if (setupCore)
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("🐛 Options de Debug", EditorStyles.boldLabel);
                
                debugMode = EditorGUILayout.Toggle("Mode Debug activé", debugMode);
                EditorGUILayout.HelpBox(
                    "Active les logs détaillés pour le debugging. Désactiver en production.",
                    MessageType.Info
                );
                
                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
            }
            
            // === EXISTING SETUP CHECK ===
            CheckExistingSetup();
            
            // === ACTION BUTTONS ===
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("🚀 Créer Setup Complet", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("Confirmer Setup",
                    "Créer la hiérarchie complète du système M2922 ?",
                    "Créer", "Annuler"))
                {
                    CreateCompleteSetup();
                }
            }
            GUI.backgroundColor = Color.white;
            
            if (GUILayout.Button("🔗 Configurer Références", GUILayout.Height(40)))
            {
                ConfigureReferences();
            }
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("🧹 Nettoyer Setup Existant", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Confirmer Nettoyage",
                    "Supprimer TOUS les GameObjects du système M2922 ?",
                    "Supprimer", "Annuler"))
                {
                    CleanupSetup();
                }
            }
            
            GUILayout.Space(10);
            
            // === CONFIGURATION MANAGEMENT ===
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
            if (GUILayout.Button("💾 Sauvegarder Config", GUILayout.Height(30)))
            {
                SavePreferences();
            }
            
            GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
            if (GUILayout.Button("🔄 Reset Config", GUILayout.Height(30)))
            {
                ResetPreferences();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox(
                "💡 La configuration est sauvegardée automatiquement lors de la création du setup.\n" +
                "Utilisez 'Sauvegarder Config' pour enregistrer manuellement vos paramètres.",
                MessageType.Info
            );
            
            EditorGUILayout.EndScrollView();
        }
        
        private void CheckExistingSetup()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("📋 État Actuel de la Scène", EditorStyles.boldLabel);
            
            managerObj = GameObject.Find("Manager");
            eventBusObj = GameObject.Find("EventBus");
            teamManagerObj = GameObject.Find("TeamManager");
            rootObject = GameObject.Find("[M2922_System]");
            
            GameObject spawnManagerObj = GameObject.Find("SpawnManager");
            
            DrawStatusLine("Root System", rootObject, true);
            if (setupCore)
                DrawStatusLine("Manager", managerObj, true);
            if (setupEventBus)
            {
                DrawStatusLine("EventBus", eventBusObj, true);
                if (eventBusObj != null)
                {
                    var poolSlots = eventBusObj.GetComponentsInChildren<M2922.Core.M2922_EventData>();
                    DrawPoolSlotsStatus("EventData Pool", poolSlots.Length, eventPoolSize);
                }
                else
                {
                    DrawPoolSlotsStatus("EventData Pool", 0, eventPoolSize);
                }
            }
            if (setupTeams)  // Seulement afficher si le système d'équipes est activé
                DrawStatusLine("TeamManager", teamManagerObj, true);
            if (setupSpawning)  // Seulement afficher si le système de spawn est activé
                DrawStatusLine("SpawnManager", spawnManagerObj, true);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatusLine(string label, GameObject obj, bool required)
        {
            EditorGUILayout.BeginHorizontal();
            
            if (obj != null)
            {
                EditorGUILayout.LabelField("✓", GUILayout.Width(20));
                EditorGUILayout.LabelField(label, GUILayout.Width(150));
                EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
            }
            else
            {
                if (required)
                {
                    EditorGUILayout.LabelField("✗", GUILayout.Width(20));
                    EditorGUILayout.LabelField(label + " (manquant)", GUILayout.Width(150));
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void CreateCompleteSetup()
        {
            Debug.Log("[M2922 Setup] Début du setup...");
            
            // Sauvegarder la configuration actuelle
            SavePreferences();
            
            // Créer root si nécessaire
            if (rootObject == null)
            {
                rootObject = new GameObject("[M2922_System]");
                Undo.RegisterCreatedObjectUndo(rootObject, "Create M2922 Root");
            }
            
            // CORE
            if (setupCore)
            {
                CreateManager();
                
                if (setupEventBus)
                {
                    CreateEventBus();
                }
            }
            
            // MODULES
            if (setupTeams)
            {
                CreateTeamManager();
            }
            
            // PLAYER OBJECT
            if (setupPlayer)
            {
                CreatePlayerObject();
            }
            
            // SPAWNING (avec ou sans teams)
            if (setupSpawning)
            {
                CreateSpawnManager();
                
                if (setupTeams)
                {
                    CreateTeamSpawnPoints();
                }
                else
                {
                    CreateGenericSpawnPoints();
                }
            }
            
            // Configure toutes les références
            ConfigureReferences();
            
            // Sélectionner le root
            Selection.activeGameObject = rootObject;
            
            Debug.Log("[M2922 Setup] Setup terminé avec succès !");
        }
        
        private void CreateManager()
        {
            if (managerObj != null)
            {
                Debug.LogWarning("[M2922 Setup] Manager existe déjà, skip...");
                return;
            }
            
            managerObj = new GameObject("Manager");
            managerObj.transform.SetParent(rootObject.transform);
            
            var manager = managerObj.AddUdonSharpComponent<M2922.Core.M2922_Manager>();
            
            // Configuration debug uniquement
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("DEBUG").boolValue = debugMode;
            so.ApplyModifiedProperties();
            
            Undo.RegisterCreatedObjectUndo(managerObj, "Create Manager");
            Debug.Log($"[M2922 Setup] Manager créé (Core minimal - Debug: {debugMode})");
        }
        
        private void CreateEventBus()
        {
            if (eventBusObj != null)
            {
                Debug.LogWarning("[M2922 Setup] EventBus existe déjà, skip...");
                return;
            }
            
            eventBusObj = new GameObject("EventBus");
            eventBusObj.transform.SetParent(rootObject.transform);
            
            var eventBus = eventBusObj.AddUdonSharpComponent<M2922.Core.M2922_EventBus>();
            
            // Configuration par défaut
            SerializedObject so = new SerializedObject(eventBus);
            so.FindProperty("maxListenersPerEvent").intValue = 20;
            so.FindProperty("_maxEventTypeValue").intValue = ComputeMaxEventTypeValue();
            so.ApplyModifiedProperties();
            
            // Créer les slots du pool EventData comme enfants de l'EventBus
            for (int i = 0; i < eventPoolSize; i++)
            {
                var slotGO = new GameObject($"Slot_{i:D2}");
                slotGO.transform.SetParent(eventBusObj.transform);
                slotGO.AddUdonSharpComponent<M2922.Core.M2922_EventData>();
                Undo.RegisterCreatedObjectUndo(slotGO, $"Create EventData Slot {i}");
            }
            
            Undo.RegisterCreatedObjectUndo(eventBusObj, "Create EventBus");
            Debug.Log($"[M2922 Setup] EventBus créé avec {eventPoolSize} slots EventData (maxEventTypeValue={ComputeMaxEventTypeValue()})");
        }
        
        private void CreateTeamManager()
        {
            if (teamManagerObj != null)
            {
                Debug.LogWarning("[M2922 Setup] TeamManager existe déjà, skip...");
                return;
            }
            
            teamManagerObj = new GameObject("TeamManager");
            teamManagerObj.transform.SetParent(rootObject.transform);
            
            var teamMgr = teamManagerObj.AddUdonSharpComponent<M2922.Teams.M2922_TeamManager>();
            
            // Configuration équipes avec les valeurs du wizard
            SerializedObject so = new SerializedObject(teamMgr);
            so.FindProperty("_autoBalance").boolValue = autoBalance;
            so.FindProperty("_allowFriendlyFire").boolValue = allowFriendlyFire;
            so.FindProperty("_friendlyFireDamageMultiplier").floatValue = friendlyFireDamageMultiplier;
            so.FindProperty("_teamCount").intValue = teamCount;
            
            // Configurer les flat arrays d'équipes
            SerializedProperty namesProp = so.FindProperty("_teamNames");
            SerializedProperty colorsProp = so.FindProperty("_teamColors");
            SerializedProperty activeProp = so.FindProperty("_teamIsActive");
            
            namesProp.arraySize = teamCount;
            colorsProp.arraySize = teamCount;
            activeProp.arraySize = teamCount;
            
            for (int i = 0; i < teamCount; i++)
            {
                namesProp.GetArrayElementAtIndex(i).stringValue = customTeamNames[i];
                colorsProp.GetArrayElementAtIndex(i).colorValue = customTeamColors[i];
                activeProp.GetArrayElementAtIndex(i).boolValue = true;
            }
            
            so.ApplyModifiedProperties();
            
            Undo.RegisterCreatedObjectUndo(teamManagerObj, "Create TeamManager");
            Debug.Log($"[M2922 Setup] TeamManager créé avec {teamCount} équipes personnalisées");
        }
        
        private void CreateSpawnManager()
        {
            GameObject spawnManagerObj = GameObject.Find("SpawnManager");
            if (spawnManagerObj != null)
            {
                Debug.LogWarning("[M2922 Setup] SpawnManager existe déjà, skip...");
                return;
            }
            
            spawnManagerObj = new GameObject("SpawnManager");
            spawnManagerObj.transform.SetParent(rootObject.transform);
            
            var spawnMgr = spawnManagerObj.AddUdonSharpComponent<M2922.Spawning.M2922_SpawnManager>();
            
            // Configuration avec les paramètres du wizard
            SerializedObject so = new SerializedObject(spawnMgr);
            so.FindProperty("_spawnStrategy").enumValueIndex = spawnStrategy;
            so.FindProperty("_spawnProtectionDuration").floatValue = spawnProtectionDuration;
            so.FindProperty("_spawnCooldown").floatValue = spawnCooldown;
            so.FindProperty("_minEnemyDistance").floatValue = minEnemyDistance;
            so.FindProperty("_autoDiscoverSpawnPoints").boolValue = autoDiscoverSpawns;
            so.FindProperty("_autoRespawn").boolValue = autoRespawn;
            so.FindProperty("_respawnDelay").floatValue = respawnDelay;
            so.ApplyModifiedProperties();
            
            Undo.RegisterCreatedObjectUndo(spawnManagerObj, "Create SpawnManager");
            Debug.Log("[M2922 Setup] SpawnManager créé avec configuration personnalisée");
        }
        
        private void CreateTeamSpawnPoints()
        {
            GameObject spawnsRoot = GameObject.Find("SpawnPoints");
            if (spawnsRoot == null)
            {
                spawnsRoot = new GameObject("SpawnPoints");
                spawnsRoot.transform.SetParent(rootObject.transform);
                Undo.RegisterCreatedObjectUndo(spawnsRoot, "Create SpawnPoints Root");
            }
            
            // Créer spawns pour toutes les équipes dynamiquement
            for (int i = 0; i < teamCount; i++)
            {
                string teamName = customTeamNames[i];
                Color teamColor = customTeamColors[i];
                
                // Position en cercle autour de l'origine
                float angle = (360f / teamCount) * i * Mathf.Deg2Rad;
                float distance = 15f; // Distance from origin
                Vector3 basePosition = new Vector3(
                    Mathf.Cos(angle) * distance,
                    0,
                    Mathf.Sin(angle) * distance
                );
                
                CreateTeamSpawns(teamName, teamColor, i, spawnsRoot, basePosition);
            }
            
            Debug.Log($"[M2922 Setup] {teamCount} équipes de spawn points créées");
        }
        
        private void CreateTeamSpawns(string teamName, Color teamColor, int teamIndex, GameObject parent, Vector3 basePosition)
        {
            GameObject teamSpawns = new GameObject($"Spawns_{teamName}");
            teamSpawns.transform.SetParent(parent.transform);
            teamSpawns.transform.position = basePosition;
            
            for (int i = 0; i < spawnPointsPerTeam; i++)
            {
                GameObject spawn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spawn.name = $"Spawn_{teamName}_{i + 1}";
                spawn.transform.SetParent(teamSpawns.transform);
                
                // Position en cercle
                float angle = (360f / spawnPointsPerTeam) * i * Mathf.Deg2Rad;
                float radius = 3f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                spawn.transform.localPosition = offset;
                spawn.transform.localScale = new Vector3(1f, 0.1f, 1f);
                
                // Couleur de l'équipe
                var renderer = spawn.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Standard"));
                mat.color = teamColor;
                renderer.material = mat;
                
                // Tag pour identification
                spawn.tag = $"Respawn";
                
                // Ajouter le composant M2922_SpawnPoint
                var spawnPoint = spawn.AddUdonSharpComponent<M2922.Spawning.M2922_SpawnPoint>();
                SerializedObject spawnSO = new SerializedObject(spawnPoint);
                spawnSO.FindProperty("_teamIndex").intValue = teamIndex;
                spawnSO.FindProperty("_gizmoColor").colorValue = teamColor;
                spawnSO.ApplyModifiedProperties();
                
                Undo.RegisterCreatedObjectUndo(spawn, $"Create {spawn.name}");
            }
            
            Undo.RegisterCreatedObjectUndo(teamSpawns, $"Create Team Spawns {teamName}");
        }
        
        private void CreateGenericSpawnPoints()
        {
            GameObject spawnsRoot = GameObject.Find("SpawnPoints");
            if (spawnsRoot == null)
            {
                spawnsRoot = new GameObject("SpawnPoints");
                spawnsRoot.transform.SetParent(rootObject.transform);
            }
            
            int numSpawns = spawnPointsPerTeam; // Utiliser la même variable
            
            for (int i = 0; i < numSpawns; i++)
            {
                GameObject spawn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spawn.name = $"SpawnPoint_{i + 1}";
                spawn.transform.SetParent(spawnsRoot.transform);
                
                float angle = (360f / numSpawns) * i * Mathf.Deg2Rad;
                float radius = 5f;
                spawn.transform.position = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                spawn.transform.localScale = new Vector3(1f, 0.1f, 1f);
                spawn.tag = "Respawn";
                
                // Ajouter le composant M2922_SpawnPoint (FFA mode: teamIndex = -1)
                var spawnPoint = spawn.AddUdonSharpComponent<M2922.Spawning.M2922_SpawnPoint>();
                SerializedObject spawnSO = new SerializedObject(spawnPoint);
                spawnSO.FindProperty("_teamIndex").intValue = -1; // FFA mode
                spawnSO.FindProperty("_gizmoColor").colorValue = Color.cyan;
                spawnSO.ApplyModifiedProperties();
                
                Undo.RegisterCreatedObjectUndo(spawn, "Create Generic Spawn Point");
            }
            
            Undo.RegisterCreatedObjectUndo(spawnsRoot, "Create Spawn Points");
            Debug.Log($"[M2922 Setup] {numSpawns} Spawn Points génériques créés (mode FFA)");
        }
        
        private void ConfigureReferences()
        {
            Debug.Log("[M2922 Setup] Configuration des références...");
            
            // Refresh references
            managerObj = GameObject.Find("Manager");
            eventBusObj = GameObject.Find("EventBus");
            teamManagerObj = GameObject.Find("TeamManager");
            
            if (managerObj == null)
            {
                Debug.LogWarning("[M2922 Setup] Manager introuvable, impossible de configurer les références");
                return;
            }
            
            var manager = managerObj.GetComponent<M2922.Core.M2922_Manager>();
            if (manager == null)
            {
                Debug.LogWarning("[M2922 Setup] Component Manager introuvable");
                return;
            }
            
            SerializedObject managerSO = new SerializedObject(manager);
            
            // Lier EventBus au Manager
            if (eventBusObj != null)
            {
                var eventBus = eventBusObj.GetComponent<M2922.Core.M2922_EventBus>();
                if (eventBus != null)
                {
                    managerSO.FindProperty("EventBus").objectReferenceValue = eventBus;
                    Debug.Log("[M2922 Setup] EventBus lié au Manager");
                    
                    // EventBus a aussi besoin du Manager
                    SerializedObject eventBusSO = new SerializedObject(eventBus);
                    eventBusSO.FindProperty("Manager").objectReferenceValue = manager;
                    
                    // Assigner le pool de slots EventData
                    var poolSlots = eventBusObj.GetComponentsInChildren<M2922.Core.M2922_EventData>();
                    SerializedProperty poolProp = eventBusSO.FindProperty("_pool");
                    if (poolProp != null && poolSlots.Length > 0)
                    {
                        poolProp.arraySize = poolSlots.Length;
                        for (int i = 0; i < poolSlots.Length; i++)
                            poolProp.GetArrayElementAtIndex(i).objectReferenceValue = poolSlots[i];
                        Debug.Log($"[M2922 Setup] {poolSlots.Length} slots assignés au pool EventBus");
                    }
                    
                    eventBusSO.ApplyModifiedProperties();
                }
            }
            
            managerSO.ApplyModifiedProperties();
            
            // Configurer TeamManager
            if (teamManagerObj != null)
            {
                var teamMgr = teamManagerObj.GetComponent<M2922.Teams.M2922_TeamManager>();
                if (teamMgr != null)
                {
                    SerializedObject teamSO = new SerializedObject(teamMgr);
                    teamSO.FindProperty("Manager").objectReferenceValue = manager;
                    
                    teamSO.ApplyModifiedProperties();
                    Debug.Log("[M2922 Setup] TeamManager configuré");
                }
            }
            
            // Configurer SpawnManager
            GameObject spawnManagerObj = GameObject.Find("SpawnManager");
            if (spawnManagerObj != null)
            {
                var spawnMgr = spawnManagerObj.GetComponent<M2922.Spawning.M2922_SpawnManager>();
                if (spawnMgr != null)
                {
                    SerializedObject spawnSO = new SerializedObject(spawnMgr);
                    
                    // Lier au Manager
                    spawnSO.FindProperty("Manager").objectReferenceValue = manager;
                    
                    // Lier au TeamManager si en mode team
                    if (teamManagerObj != null)
                    {
                        var teamMgr = teamManagerObj.GetComponent<M2922.Teams.M2922_TeamManager>();
                        spawnSO.FindProperty("_teamManager").objectReferenceValue = teamMgr;
                    }
                    
                    spawnSO.ApplyModifiedProperties();
                    Debug.Log("[M2922 Setup] SpawnManager configuré");
                }
            }
            
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
            
            Debug.Log("[M2922 Setup] Références configurées avec succès");
        }
        
        private void CreatePlayerObject()
        {
            if (GameObject.Find("PlayerObject") != null)
            {
                Debug.LogWarning("[M2922 Setup] PlayerObject existe déjà, skip...");
                return;
            }
            
            var playerGO = new GameObject("PlayerObject");
            playerGO.transform.SetParent(rootObject.transform);
            
            // M2922_HealthController
            var health = playerGO.AddUdonSharpComponent<M2922.Combat.M2922_HealthController>();
            SerializedObject healthSO = new SerializedObject(health);
            healthSO.FindProperty("_entityType").enumValueIndex = 1; // Player
            healthSO.FindProperty("_maxHealth").floatValue = playerMaxHealth;
            healthSO.FindProperty("_regenerateHealth").boolValue = playerRegen;
            healthSO.FindProperty("_regenRate").floatValue = playerRegenRate;
            healthSO.FindProperty("_regenDelay").floatValue = playerRegenDelay;
            healthSO.FindProperty("_headshotMultiplier").floatValue = playerHeadshotMultiplier;
            healthSO.FindProperty("_autoRespawnDelay").floatValue = playerAutoRespawnDelay;
            healthSO.FindProperty("_disableOnDeath").boolValue = playerDisableOnDeath;
            healthSO.ApplyModifiedProperties();
            
            // M2922_PlayerController
            var controller = playerGO.AddUdonSharpComponent<M2922.Player.M2922_PlayerController>();
            SerializedObject controllerSO = new SerializedObject(controller);
            
            // Lier le Manager
            if (managerObj != null)
            {
                var manager = managerObj.GetComponent<M2922.Core.M2922_Manager>();
                if (manager != null)
                {
                    healthSO = new SerializedObject(health);
                    healthSO.FindProperty("Manager").objectReferenceValue = manager;
                    healthSO.ApplyModifiedProperties();
                    controllerSO.FindProperty("Manager").objectReferenceValue = manager;
                }
            }
            
            // Lier HealthController
            controllerSO.FindProperty("_healthController").objectReferenceValue = health;
            
            // Lier TeamManager si présent
            if (teamManagerObj != null)
            {
                var teamMgr = teamManagerObj.GetComponent<M2922.Teams.M2922_TeamManager>();
                if (teamMgr != null)
                    controllerSO.FindProperty("_teamManager").objectReferenceValue = teamMgr;
            }
            
            // Lier SpawnManager si présent
            GameObject spawnMgrGO = GameObject.Find("SpawnManager");
            if (spawnMgrGO != null)
            {
                var spawnMgr = spawnMgrGO.GetComponent<M2922.Spawning.M2922_SpawnManager>();
                if (spawnMgr != null)
                    controllerSO.FindProperty("_spawnManager").objectReferenceValue = spawnMgr;
            }
            
            controllerSO.ApplyModifiedProperties();
            
            // Ajouter le composant VRC Player Object
            playerGO.AddComponent<VRC.SDK3.Components.VRCPlayerObject>();
            
            Undo.RegisterCreatedObjectUndo(playerGO, "Create PlayerObject");
            Debug.Log($"[M2922 Setup] PlayerObject créé ({playerMaxHealth} HP, respawn: {playerAutoRespawnDelay}s)");
        }
        
        private void DrawPoolSlotsStatus(string label, int current, int target)
        {
            EditorGUILayout.BeginHorizontal();
            
            if (current >= target)
            {
                EditorGUILayout.LabelField("✓", GUILayout.Width(20));
                EditorGUILayout.LabelField(label, GUILayout.Width(150));
                EditorGUILayout.LabelField($"{current} slots", EditorStyles.miniLabel);
            }
            else if (current > 0)
            {
                EditorGUILayout.LabelField("⚠", GUILayout.Width(20));
                EditorGUILayout.LabelField(label, GUILayout.Width(150));
                EditorGUILayout.LabelField($"{current}/{target} slots (incomplet)", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("✗", GUILayout.Width(20));
                EditorGUILayout.LabelField(label + " (manquant)", GUILayout.Width(150));
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private int ComputeMaxEventTypeValue()
        {
            int max = 0;
            foreach (int val in System.Enum.GetValues(typeof(M2922.Core.EventType)))
            {
                if (val > max) max = val;
            }
            return max + 1;
        }
        
        private void CleanupSetup()
        {
            Debug.Log("[M2922 Setup] Nettoyage du setup...");
            
            if (rootObject != null)
            {
                Undo.DestroyObjectImmediate(rootObject);
                Debug.Log("[M2922 Setup] Root object supprimé");
            }
            
            // Nettoyer les objets orphelins
            if (managerObj != null && managerObj.transform.parent == null)
                Undo.DestroyObjectImmediate(managerObj);
            if (eventBusObj != null && eventBusObj.transform.parent == null)
                Undo.DestroyObjectImmediate(eventBusObj);
            if (teamManagerObj != null && teamManagerObj.transform.parent == null)
                Undo.DestroyObjectImmediate(teamManagerObj);
            
            managerObj = null;
            eventBusObj = null;
            teamManagerObj = null;
            rootObject = null;
            
            Debug.Log("[M2922 Setup] Nettoyage terminé");
        }
    }
}
