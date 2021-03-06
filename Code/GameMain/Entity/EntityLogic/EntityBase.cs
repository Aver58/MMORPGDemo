﻿using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameMain
{
    public class EntityBase : EntityLogic
    {
        [SerializeField] private EntityData m_EntityData = null;

        public int Id
        {
            get
            {
                return Entity.Id;
            }
        }

        public Animator CachedAnimator
        {
            get;
            private set;
        }

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            CachedAnimator = GetComponent<Animator>();
        }

        protected override void OnShow(object userData)
        {
            base.OnShow(userData);

            m_EntityData = userData as EntityData;
            if (m_EntityData == null)
            {
                Log.Error("Entity data is invalid.");
                return;
            }

            Name = string.Format("{0}[{1}][{2}]",gameObject.name,m_EntityData.TypeId,Id);
            CachedTransform.localPosition = m_EntityData.Position;
            CachedTransform.localRotation = m_EntityData.Rotation;
            CachedTransform.localScale = Vector3.one;
        }

        protected override void OnHide(object userData)
        {
            base.OnHide(userData);
        }

        protected override void OnAttached(EntityLogic childEntity, Transform parentTransform, object userData)
        {
            base.OnAttached(childEntity, parentTransform, userData);
        }

        protected override void OnDetached(EntityLogic childEntity, object userData)
        {
            base.OnDetached(childEntity, userData);
        }

        protected override void OnAttachTo(EntityLogic parentEntity, Transform parentTransform, object userData)
        {
            base.OnAttachTo(parentEntity, parentTransform, userData);
        }

        protected override void OnDetachFrom(EntityLogic parentEntity, object userData)
        {
            base.OnDetachFrom(parentEntity, userData);
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
        }

        public virtual void SetPosition(Vector3 Position,bool isLocal = false)
        {
            if (isLocal)
            {
                CachedTransform.localPosition = Position;
            }
            else
            {
                CachedTransform.position = Position;
            }
        }

        public virtual void SetRotation(Quaternion rotation,bool isLocal = false)
        {
            if (isLocal)
            {
                CachedTransform.localRotation = rotation;
            }
            else
            {
                CachedTransform.rotation = rotation;
            }
        }

        public virtual void SetScale(Vector3 scale)
        {
            CachedTransform.localScale = scale;
        }

    }
}
