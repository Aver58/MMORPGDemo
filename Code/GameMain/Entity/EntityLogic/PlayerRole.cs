﻿using GameFramework;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 玩家
    /// </summary>
    public class PlayerRole : RoleEntityBase
    {

        protected override void OnShow(object userData)
        {
            base.OnShow(userData);

            RoleEntityData playerEntityData = userData as RoleEntityData;
            if (playerEntityData == null)
            {
                Log.Error("playerEntityData is null");
                return;
            }

            //创建Actor
            ActorType actorType = playerEntityData.ActorType;
            BattleCampType campType = playerEntityData.CampType;
            Actor = new ActorPlayer(this, actorType, campType, m_CharacterController,
                m_Animator);
            Actor.Init();

            AddEventListener();
        }

        protected override void OnHide(object userData)
        {
            base.OnHide(userData);

            RemoveEventListener();
        }

        private void AddEventListener()
        {
            GameEntry.Input.OnAxisInput += OnPlayerMove;
            GameEntry.Input.OnAxisInputEnd += OnPlayerIdle;
        }

        private void RemoveEventListener()
        {
            GameEntry.Input.OnAxisInput -= OnPlayerMove;
            GameEntry.Input.OnAxisInputEnd -= OnPlayerIdle;
        }

        private void OnPlayerMove(Vector2 delta)
        {
            Actor.ExecuteCommand(new MoveCommand(delta));
        }

        private void OnPlayerIdle()
        {
            Actor.ExecuteCommand(new IdleCommand());
        }
    }
}
