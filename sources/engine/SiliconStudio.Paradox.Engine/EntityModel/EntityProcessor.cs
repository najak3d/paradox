﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Collections.Generic;
using System.Linq;
using SiliconStudio.Core.Diagnostics;
using SiliconStudio.Core.Extensions;
using SiliconStudio.Paradox.Games;
using System.Threading.Tasks;
using SiliconStudio.Core;

namespace SiliconStudio.Paradox.EntityModel
{
    /// <summary>Entity processor, triggered on various <see cref="EntitySystem"/> events such as Entity and Component additions and removals.</summary>
    public abstract class EntityProcessor
    {
        private bool enabled = true;

        internal ProfilingKey UpdateProfilingKey;
        internal ProfilingKey DrawProfilingKey;

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        public EntitySystem EntitySystem { get; internal set; }

        public IServiceRegistry Services { get; internal set; }

        protected EntityProcessor()
        {
            UpdateProfilingKey = new ProfilingKey(GameProfilingKeys.GameUpdate, this.GetType().Name);
            DrawProfilingKey = new ProfilingKey(GameProfilingKeys.GameDraw, this.GetType().Name);
        }

        /// <summary>
        /// Performs work related to this processor.
        /// </summary>
        /// <param name="time"></param>
        public virtual void Update(GameTime time)
        {
        }

        /// <summary>
        /// Performs work related to this processor.
        /// </summary>
        /// <param name="time"></param>
        public virtual void Draw(GameTime time)
        {
        }

        /// <summary>
        /// Run when this <see cref="EntityProcessor" /> is added to an <see cref="EntitySystem" />.
        /// </summary>
        protected internal abstract void OnSystemAdd();

        /// <summary>
        /// Run when this <see cref="EntityProcessor" /> is removed from an <see cref="EntitySystem" />.
        /// </summary>
        protected internal abstract void OnSystemRemove();

        /// <summary>
        /// Specifies weither an entity is enabled or not.
        /// </summary>
        /// <param name="entity">The entity.</param>
        protected internal abstract void SetEnabled(Entity entity, bool enabled);

        protected virtual void OnEnabledChanged(Entity entity, bool enabled)
        {
            
        }

        /// <summary>
        /// Checks if <see cref="Entity"/> needs to be either added or removed.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="forceRemove">if set to <c>true</c> [force remove].</param>
        protected internal abstract void EntityCheck(Entity entity, List<EntityProcessor> processors, bool forceRemove = false);

        /// <summary>
        /// Adds the entity to the internal list of the <see cref="EntitySystem"/>.
        /// Exposed for inheriting class that has no access to EntitySystem as internal.
        /// </summary>
        /// <param name="entity">The entity.</param>
        protected internal void InternalAddEntity(Entity entity)
        {
            EntitySystem.InternalAddEntity(entity);
        }

        /// <summary>
        /// Removes the entity to the internal list of the <see cref="EntitySystem"/>.
        /// Exposed for inheriting class that has no access to EntitySystem as internal.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="removeParent">Indicate if entity should be removed from its parent</param>
        protected internal void InternalRemoveEntity(Entity entity, bool removeParent)
        {
            EntitySystem.InternalRemoveEntity(entity, removeParent);
        }
    }

    /// <summary>Helper class for <see cref="EntityProcessor"/>, that will keep track of <see cref="Entity"/> matching certain <see cref="EntityComponent"/> requirements.</summary>
    /// Additional precomputed data will be stored alongside the <see cref="Entity"/> to offer faster accesses and iterations.
    /// <typeparam name="T">Generic type parameter.</typeparam>
    public abstract class EntityProcessor<T> : EntityProcessor
    {
        protected Dictionary<Entity, T> enabledEntities = new Dictionary<Entity, T>();
        protected Dictionary<Entity, T> matchingEntities = new Dictionary<Entity, T>();
        protected HashSet<Entity> reentrancyCheck = new HashSet<Entity>();
        protected PropertyKey[] requiredKeys;

        protected EntityProcessor(PropertyKey[] requiredKeys)
        {
            this.requiredKeys = requiredKeys;
        }

        /// <summary>Gets the required components for an entity to be added to this entity processor.</summary>
        /// <value>The required keys.</value>
        protected virtual PropertyKey[] RequiredKeys
        {
            get { return requiredKeys; }
        }

        /// <inheritdoc/>
        protected internal override void OnSystemAdd()
        {
        }

        /// <inheritdoc/>
        protected internal override void OnSystemRemove()
        {
        }

        /// <inheritdoc/>
        protected internal override void SetEnabled(Entity entity, bool enabled)
        {
            if (enabled)
            {
                T entityData;
                if (!matchingEntities.TryGetValue(entity, out entityData))
                    throw new InvalidOperationException("EntityProcessor: Tried to enable an unknown entity.");

                enabledEntities.Add(entity, matchingEntities[entity]);
            }
            else
            {
                if (!enabledEntities.Remove(entity))
                    throw new InvalidOperationException("Invalid Entity Enabled state");
            }

            OnEnabledChanged(entity, enabled);
        }

        /// <inheritdoc/>
        protected internal override void EntityCheck(Entity entity, List<EntityProcessor> processors, bool forceRemove)
        {
            // If forceRemove is true, no need to check if entity matches.
            bool entityMatch = !forceRemove && EntityMatch(entity);
            T entityData;
            bool entityAdded = matchingEntities.TryGetValue(entity, out entityData);

            if (entityMatch && !entityAdded)
            {
                // Adding entity is not reentrant, so let's skip if already being called for current entity
                // (could happen if either GenerateAssociatedData, OnEntityPrepare or OnEntityAdd changes
                // any Entity components
                lock (reentrancyCheck)
                {
                    if (!reentrancyCheck.Add(entity))
                        return;
                }
                
                // Need to add entity
                entityData = GenerateAssociatedData(entity);

                processors.Add(this);
                OnEntityAdding(entity, entityData);
                matchingEntities.Add(entity, entityData);

                // If entity was enabled, add it to enabled entity list
                if (EntitySystem.IsEnabled(entity))
                    enabledEntities.Add(entity, entityData);

                lock (reentrancyCheck)
                {
                    reentrancyCheck.Remove(entity);
                }
            }
            else if (entityAdded && !entityMatch)
            {
                // Need to be removed
                OnEntityRemoved(entity, entityData);
                processors.SwapRemove(this);

                // Remove from enabled and matching entities
                enabledEntities.Remove(entity);
                matchingEntities.Remove(entity);
            }
        }

        /// <summary>Generates associated data to the given entity.</summary>
        /// Called right before <see cref="OnEntityAdding"/>.
        /// <param name="entity">The entity.</param>
        /// <returns>The associated data.</returns>
        protected abstract T GenerateAssociatedData(Entity entity);

        protected virtual bool EntityMatch(Entity entity)
        {
            return RequiredKeys.All(x => entity.Tags.Get(x) != null);
        }

        protected virtual void EntityReadd(Entity entity)
        {
            T data;
            if (matchingEntities.TryGetValue(entity, out data))
            {
                try
                {
                    OnEntityRemoved(entity, data);
                    OnEntityAdding(entity, data);
                }
                catch (Exception)
                {
                    enabledEntities.Remove(entity);
                    matchingEntities.Remove(entity);
                    throw new Exception("Error during entity readd.");
                }
            }
        }
        
        /// <summary>Run when a matching entity is added to this entity processor.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="data">  The associated data.</param>
        protected virtual void OnEntityAdding(Entity entity, T data)
        {
        }

        /// <summary>Run when a matching entity is removed from this entity processor.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="data">  The associated data.</param>
        protected virtual void OnEntityRemoved(Entity entity, T data)
        {
        }
    }
}