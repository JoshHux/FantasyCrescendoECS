﻿using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Unity.Core;
using Unity.Transforms;
using Unity.Assertions;
using Unity.Entities;

namespace HouraiTeahouse.FantasyCrescendo.Matches {

public class MatchInitializationSettings {
  public Transform[] SpawnPoints;
  public Transform[] RespawnPoints;
}

public abstract class Match : IDisposable {

  public MatchConfig Config { get; protected set; }

  protected World World { get; }
  protected EntityManager EntityManager => World.EntityManager;
  protected ComponentSystemGroup Simulation { get; }

  BlobAssetStore _blobAssetStore;

  protected Match(MatchConfig config, World world = null) {
    Config = config;
    World = world ?? Unity.Entities.World.DefaultGameObjectInjectionWorld;

    Simulation = World.GetOrCreateSystem<SimulationSystemGroup>();
    Simulation.Enabled = false;
    Simulation.SortSystems();
    
    _blobAssetStore = new BlobAssetStore();
  }

  public async Task Initialize(MatchInitializationSettings settings) {
    SetupMatchRules();
    SetupStage(settings);

    EntityManager.CreateEntity(ComponentType.ReadOnly<MatchState>());
    Simulation.SetSingleton(Config.CreateInitialState());

    await SpawnPlayers(settings);
  }

  protected void Step() {
    World.PushTime(new TimeData(Time.fixedTime, Time.fixedDeltaTime));
    Simulation.Enabled = true;
    SampleLocalInputs();
    Simulation.Update();
    Simulation.Enabled = false;
    World.PopTime();
  }

  public virtual void Update() => Step();
  public virtual void Dispose() {
    _blobAssetStore?.Dispose();
    _blobAssetStore = null;
  }

  protected abstract IEnumerable<Type> GetRuleTypes();

  void SampleLocalInputs() {
    var manager = InputManager.Instance;
    var system = World?.GetOrCreateSystem<InjectInputsSystem>();
    if (manager == null || system == null) return;
    for (var i = 0; i < Config.PlayerCount; i++) {
      if (!Config[i].IsLocal) continue;
      var sampledInput = manager.GetInputForPlayer(Config[i].LocalPlayerID);
      system.SetPlayerInput(Config[i].PlayerID, sampledInput);
    }
  }

  void SetupMatchRules() {
    var enabledRules = new HashSet<Type>(GetRuleTypes());
    foreach (var system in World.GetExistingSystem<MatchRuleSystemGroup>().Systems) {
      if (system.Enabled = enabledRules.Contains(system.GetType())) {
        Debug.Log($"Enabled match rule: {system}");
      }
    }
  }

  void SetupStage(MatchInitializationSettings settings) {
    var archetype = EntityManager.CreateArchetype(
      ComponentType.ReadWrite<Translation>(),
      ComponentType.ReadWrite<RespawnPoint>()
    );
    foreach (var point in settings.RespawnPoints) {
      var entity = EntityManager.CreateEntity(archetype);
      EntityManager.AddComponentData(entity, new Translation { Value = point.position });
    }
  }

  async Task SpawnPlayers(MatchInitializationSettings settings) {
    await DataLoader.WaitUntilLoaded();
    var spawnPoints = settings.SpawnPoints;
    var tasks = new Task<GameObject>[Config.PlayerCount];
    for (var i = 0; i < Config.PlayerCount; i++) {
      Transform spawnPoint = spawnPoints[i % spawnPoints.Length];
      tasks[i] = LoadPlayerGameObject(Config[i], spawnPoint);
    }
    var playerGos = await Task.WhenAll(tasks);
    for (var i = 0; i < playerGos.Length; i++) {
      ToPlayerEntity(Config[i], playerGos[i]);
    }
    Debug.Log("Players spawned!");
  }

  async Task<GameObject> LoadPlayerGameObject(PlayerConfig config, Transform transform) {
    var pallete = config.Selection.GetPallete();
    var prefab = await pallete.Prefab.LoadAssetAsync<GameObject>().Task;
    var player = GameObject.Instantiate(prefab, transform.position, Quaternion.identity);
#if UNITY_EDITOR
    player.name = $"Player {config.PlayerID + 1} ({prefab.name})";
#endif
    return player;
  }

  Entity ToPlayerEntity(PlayerConfig playerConfig, GameObject player) {
    var settings = new GameObjectConversionSettings(World, 
                        GameObjectConversionUtility.ConversionFlags.AssignName, 
                        _blobAssetStore);
    var entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(player, settings);
    EntityManager.AddComponentData(entity, playerConfig);
    EntityManager.AddComponentData(entity, new PlayerComponent {
      RNG = new Unity.Mathematics.Random((uint)(Config.RandomSeed ^ (1 << playerConfig.PlayerID))),
      Stocks = (int)Config.Stocks,
      Damage = playerConfig.DefaultDamage,
    });
    UnityEngine.Object.Destroy(player);
    Debug.Log($"Player {playerConfig.PlayerID} spawned!");
    return entity;
  }

}

}