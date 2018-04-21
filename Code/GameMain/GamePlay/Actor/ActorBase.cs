﻿using System;
using System.Collections.Generic;
using GameFramework;
using GameFramework.Fsm;
using UnityEngine;

namespace GameMain
{
    /// <summary>
    /// 行动者基类
    /// </summary>
    public partial class ActorBase : IActor
    {
        #region Property

        public Transform CachedTransform { get; }

        public Transform BornParam { get; }

        public GameObject EntityGo { get; }

        public int Id { get; }

        public int EntityId { get; }

        public ActorType ActorType { get; }

        public PartnerSortType Sort { get; set; }

        public BattleCampType Camp { get; }

        public IAttribute Attrbute => m_CurAttribute;

        public ActorBuff ActorBuff => m_ActorBuff;

        public ActorBase Target
        {
            get
            {
                if (m_Target == null)
                {
                    Log.Error("Target is null");
                    return null;
                }

               // return m_Target;
                return this;
            }
        }

        public ActorFsmStateType CurFsmStateType
        {
            get
            {
                if (m_ActorFsm == null)
                {
                    return ActorFsmStateType.FSM_BORN;
                }
                ActorFsmStateBase state = m_ActorFsm.CurrentState as ActorFsmStateBase;

                if (state != null)
                    return state.StateType;
                else
                    return ActorFsmStateType.FSM_BORN;
            }
        }

        public Vector3 Dir => CachedTransform.forward;

        public Vector3 Euler => CachedTransform.localEulerAngles;

        public Vector3 Pos => CachedTransform.position;

        public float Height => m_CharacterController.height * CachedTransform.localScale.x;

        public float Radius => m_CharacterController.radius * CachedTransform.localScale.x;

        public bool IsDead => CurFsmStateType == ActorFsmStateType.FSM_DEAD;

        #endregion

        protected Dictionary<AIFeatureType, bool> m_AIFeatures = new Dictionary<AIFeatureType, bool>();
        protected Dictionary<ActorStateType, bool> m_ActorStates = new Dictionary<ActorStateType, bool>();

        protected List<ActorBase> m_Enemys = new List<ActorBase>();
        protected List<ActorBase> m_Allys = new List<ActorBase>();
        protected List<ActorBase> m_Targets = new List<ActorBase>();

        protected CharacterController m_CharacterController;
        protected Animator m_Animator;
        protected Transform[] m_Hands;

        protected ActorBase m_Target;
        protected ActorBase m_Host;
        protected ActorBuff m_ActorBuff;
        protected ActorCard m_ActorCard;
        protected ActorAttribute m_BaseAttribute;
        protected ActorAttribute m_CurAttribute;
        protected IFsm<ActorBase> m_ActorFsm;
        protected IActorAI m_ActorAI;
        protected IBuffCtrl m_BuffCtrl;
        protected IActorSkill m_ActorSkill;
        protected IAnimController m_AnimController;
        protected IAIPathFinding m_ActorPathFinding;
        protected ICommandReceiver m_CommandReceiver;

        protected DRActorEntity m_ActorData = null;
        protected int m_CurSkillId = 0;



        protected ActorBase(int entityId,int id, GameObject go, ActorType type, BattleCampType camp,
            CharacterController cc, Animator anim)
        {
            if (id == 0 || go == null || cc == null || anim == null)
            {
                throw new GameFrameworkException("Construct Actor Fail.");
            }

            Id = id;
            EntityId = entityId;
            ActorType = type;
            Camp = camp;
            EntityGo = go;
            CachedTransform = go.transform;
            BornParam = go.transform;
            m_CharacterController = cc;
            m_Animator = anim;

            m_ActorData = GameEntry.DataTable.GetDataTable<DRActorEntity>().GetDataRow(id);
            m_ActorSkill = new ActorSkill(this);
            m_ActorBuff = new ActorBuff(this);
        }


        public virtual void Init()
        {
            InitAttribute(true);
            InitLayer();
            InitCommands();
            InitAnim();
            InitFeature();
            InitState();
            InitFsm();
            InitAi();

            CreateBoard();
        }

        public virtual void Step()
        {
            if (CachedTransform == null || m_ActorFsm == null || m_ActorAI == null || m_ActorPathFinding == null)
            {
                return;
            }

            m_ActorAI.Step();
            m_ActorSkill.Step();
            m_AnimController.Step();
            m_ActorPathFinding.Step();
        }

        public virtual void Pause(bool isPause)
        {
            m_AnimController.SetEnable(!isPause);
            if(!isPause)
                ChangeState<ActorIdleFsm>();
        }

        public virtual void Destory()
        {
            m_ActorAI.Clear();         
        }

        public virtual void UpdateCurAttribute(bool init = false)
        {
            ActorAttribute bfAttr = new ActorAttribute();
            ActorAttribute bpAttr = new ActorAttribute();
            Map<int, BuffBase> buffs = m_ActorBuff.GetAllBuff();
            for (buffs.Begin(); buffs.Next();)
            {
                for (int i = 0; i < buffs.Value.AttrList.Count; i++)
                {
                    BuffAttrData attrData = buffs.Value.AttrList[i];
                    switch (attrData.ValueType)
                    {
                        case DataValueType.Fix:
                            bfAttr.UpdateValue(attrData.AttrType, attrData.Value);
                            break;
                        case DataValueType.Per:
                            bpAttr.UpdateValue(attrData.AttrType, attrData.Value);
                            break;
                    }
                }
            }

            int maxHp = (m_BaseAttribute.MaxHp + bfAttr.MaxHp) * (1 + bpAttr.MaxHp / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.MaxHp, maxHp);

            int maxMp = (m_BaseAttribute.MaxMp + bfAttr.MaxMp) * (1 + bpAttr.MaxMp / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.MaxMp, maxMp);

            int attack = (m_BaseAttribute.Attack + bfAttr.Attack) * (1 + bpAttr.Attack / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.Attack, attack);

            int defense = (m_BaseAttribute.Defense + bfAttr.Defense) * (1 + bpAttr.Defense / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.Defense, defense);

            int absorb = (m_BaseAttribute.Absorb + bfAttr.Absorb) * (1 + bpAttr.Absorb / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.Absorb, absorb);

            int speed = (m_BaseAttribute.Speed + bfAttr.Speed) * (1 + bpAttr.Speed / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.Speed, speed);

            int reflex = (m_BaseAttribute.Reflex + bfAttr.Reflex) * (1 + bpAttr.Reflex / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.Reflex, reflex);

            int suckBlood = (m_BaseAttribute.SuckBlood + bfAttr.SuckBlood) * (1 + bpAttr.SuckBlood / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.SuckBlood, suckBlood);

            int crit = (m_BaseAttribute.Crit + bfAttr.Crit) * (1 + bpAttr.Crit / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.Crit,crit);

            int critDamage = (int)((m_BaseAttribute.CritDamage + bfAttr.CritDamage) * (1 + bpAttr.CritDamage / Constant.Define.PerBase));
            m_CurAttribute.UpdateValue(AttributeType.CritDamage, critDamage);

            int dodge = (m_BaseAttribute.Dodge + bfAttr.Dodge) * (1 + bpAttr.Dodge / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.Dodge, dodge);

           int hit = (m_BaseAttribute.Hit + bfAttr.Hit) * (1 + bpAttr.Hit / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.Hit, hit);

            int mpRecover = (m_BaseAttribute.MpRecover + bfAttr.MpRecover) * (1 + bpAttr.MpRecover / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.MpRecover, mpRecover);

            int hpRecover = (m_BaseAttribute.HpRecover + bfAttr.HpRecover) * (1 + bpAttr.HpRecover / Constant.Define.PerBase);
            m_CurAttribute.UpdateValue(AttributeType.MpRecover, hpRecover);

            if (init)
            {
                m_CurAttribute.UpdateValue(AttributeType.Hp, m_BaseAttribute.Hp);
                m_CurAttribute.UpdateValue(AttributeType.Mp, m_BaseAttribute.Mp);
            }
            else
            {
                int hp = m_CurAttribute.Hp > m_BaseAttribute.MaxHp ? m_BaseAttribute.MaxHp : m_CurAttribute.Hp;
                int mp = m_CurAttribute.Mp > m_BaseAttribute.MaxMp ? m_BaseAttribute.MaxMp : m_CurAttribute.Mp;

                m_CurAttribute.UpdateValue(AttributeType.MaxHp, hp);
                m_CurAttribute.UpdateValue(AttributeType.MaxMp, mp);
            }
        }


        protected virtual void UpdateHealth()
        {
            //TODO 更新血量
        }

        protected virtual void UpdatePower()
        {
            //TODO 更新魔力
        }

        public virtual int Attack(IActor defender, int value)
        {
            if (defender == null || value <= 0)
            {
                return 0;
            }
            float v = value - defender.Attrbute.GetValue(AttributeType.Defense)*0.2f;
            v = v*Constant.Define.DamageRatio;
            if (v <= 0)
            {
                v = UnityEngine.Random.Range(3, 7);
            }

            float cRate = this.Attrbute.GetValue(AttributeType.Crit)*0.01f;
            float bRate = this.Attrbute.GetValue(AttributeType.CritDamage)*0.01f;
            bool critical = GlobalTools.IsTrigger(cRate);
            if (critical)
            {
                v = (v*(1 + bRate));
            }
            int dmg = Mathf.FloorToInt(UnityEngine.Random.Range(0.85f, 1.08f)*v);
            defender.TakeDamage(this, dmg, critical);
            return dmg;
        }

        public virtual int SuckBlood(IActor defender, int value, float suckRate)
        {
            if (defender == null || value <= 0)
            {
                return 0;
            }
            int v = Attack(defender, value);
            if (suckRate < 0)
            {
                suckRate = 0;
            }
            int suckValue = Mathf.FloorToInt(v * suckRate);
            AddHp(suckValue, true);
            return suckValue;
        }

        public virtual void TakeDamage(IActor attacker, int damage, bool strike = false)
        {
            ShowFlyword(strike ? FlyWordType.CritHurt : FlyWordType.Hurt, damage);

            int curHp = Attrbute.GetValue(AttributeType.Hp);

            if (curHp > damage)
            {
                Attrbute.UpdateValue(AttributeType.Hp, curHp - damage);
            }
            else
            {
                Attrbute.UpdateValue(AttributeType.Hp, 0);
            }
            UpdateHealth();
            if (curHp <= 0)
            {
                ExecuteCommand(new DeadCommand(ActorDeadType.Normal));
            }
        }

        public virtual void SetTarget(ActorBase actor)
        {
            if (actor == null)
            {
                m_Target = null;
                return;
            }
            if (m_Target == actor)
            {
                return;
            }
            m_Target = actor;
            CachedTransform.LookAt(m_Target.CachedTransform);
        }

        public virtual void SetHost(ActorBase actor)
        {
            m_Host = actor;
        }

        public virtual void ChangeState<T>(ICommand command = null) where T : ActorFsmStateBase
        {
            if (m_ActorFsm == null)
            {
                Log.Error("Please set ActorFsm first");
                return;
            }

            if (!m_ActorFsm.HasState<T>())
            {
                Log.Error("Can no find state" + typeof(T));
                return;
            }

            ActorFsmStateBase state = m_ActorFsm.GetState<T>();

            if (CurFsmStateType == ActorFsmStateType.FSM_DEAD && state.StateType != ActorFsmStateType.FSM_REBORN)
            {
                return;
            }

            m_ActorFsm.GetState<T>().SetCommand(command);
            state.ChangeState<T>();
        }

        public virtual void TranslateTo(Vector3 destPosition, bool idle)
        {
            if (CachedTransform == null)
            {
                return;
            }
            CachedTransform.position = destPosition;
            if (idle)
            {
                ChangeState<ActorEmptyFsm>();
            }
        }

        public virtual void MoveTo(Vector3 destPosition)
        {
            this.SetActorState(ActorStateType.IsAutoToMove, true);
            m_ActorPathFinding.SetDestPosition(destPosition);
        }

        public virtual void StopPathFinding()
        {
            //this.SetActorState(ActorStateType.IsAutoToMove, false);
            //m_ActorPathFinding.StopPathFinding();
        }

        public virtual void SetAlphaVertexColorOff(float time)
        {
           //TODO: 模型渐出
        }

        public virtual void SetAlphaVertexColorOn(float time)
        {
            //TODO: 模型渐入
        }

        public virtual bool CannotControlSelf()
        {
            switch (CurFsmStateType)
            {
                case ActorFsmStateType.FSM_STUN:
                case ActorFsmStateType.FSM_FROST:
                case ActorFsmStateType.FSM_FEAR:
                case ActorFsmStateType.FSM_BEATFLY:
                case ActorFsmStateType.FSM_BEATDOWN:
                case ActorFsmStateType.FSM_BEATBACK:
                case ActorFsmStateType.FSM_DROP:
                case ActorFsmStateType.FSM_DEAD:
                case ActorFsmStateType.FSM_FLOATING:
                case ActorFsmStateType.FSM_HOOK:
                case ActorFsmStateType.FSM_VARIATION:
                case ActorFsmStateType.FSM_WOUND:
                case ActorFsmStateType.FSM_GRAB:
                case ActorFsmStateType.FSM_SLEEP:
                case ActorFsmStateType.FSM_PARALY:
                case ActorFsmStateType.FSM_BLIND:
                case ActorFsmStateType.FSM_JUMP:
                case ActorFsmStateType.FSM_REBORN:
                    return true;
                default:
                    return false;
            }
        }

        public virtual bool CanActManully()
        {
            switch (CurFsmStateType)
            {
                case ActorFsmStateType.FSM_IDLE:
                case ActorFsmStateType.FSM_RUN:
                case ActorFsmStateType.FSM_WALK:
                case ActorFsmStateType.FSM_SKILL:
                    return true;
                default:
                    return false;
            }
        }

        public virtual void Clear()
        {
            RemoveBoard();
            RemoveEffect();
        }



        public Transform[] GetHands()
        {
            if (m_Hands == null && CachedTransform != null)
            {
                m_Hands = new Transform[2];
                m_Hands[0] = GlobalTools.GetBone(CachedTransform, "Bip01 L Hand");
                m_Hands[1] = GlobalTools.GetBone(CachedTransform, "Bip01 R Hand");
            }
            return m_Hands;
        }

        public Vector3 GetBind(ActorBindPosType bindType, Vector3 offset)
        {
            switch (bindType)
            {
                case ActorBindPosType.Body:
                    return Pos + new Vector3(0, Height * 0.5f, 0) + offset;
                case ActorBindPosType.Head:
                    return Pos + new Vector3(0, Height, 0) + offset;
                case ActorBindPosType.Foot:
                    return Pos + offset;
                default:
                    return Pos;
            }
        }

        public IAnimController GetAnimController()
        {
            return m_AnimController;
        }

        public IAIPathFinding GetActorPathFinding()
        {
            return m_ActorPathFinding;
        }

        public void ApplyAnimator(bool enable)
        {
            if (m_AnimController == null)
            {
                Log.Error("Please set AnimController first.");
                return;
            }

            m_AnimController.SetEnable(enable);
        }

        public void ApplyRootMotion(bool enable)
        {
            if (m_AnimController == null)
            {
                Log.Error("Please set AnimController first.");
                return;
            }

            m_AnimController.SetRootMotionEnable(enable);
        }

        public void ApplyCharacterCtrl(bool enabled)
        {
            if (m_CharacterController == null)
            {
                Log.Error("CharacterController is null.");
                return;
            }
            m_CharacterController.enabled = enabled;
        }

        public void ChangeModel(int id)
        {
            //TODO 更换模型
        }

        public void GotoEmptyFsm()
        {
            ChangeState<ActorEmptyFsm>();
        }

        public void SetActorState(ActorStateType type, bool flag)
        {
            m_ActorStates[type] = flag;
        }

        public bool GetActorState(ActorStateType type)
        {
            bool flag;
            m_ActorStates.TryGetValue(type, out flag);
            return flag;
        }

        public bool GetAIFeature(AIFeatureType type)
        {
            bool flag;
            m_AIFeatures.TryGetValue(type, out flag);
            return flag;
        }

        public CommandReplyType ExecuteCommand<T>(T command) where T : ICommand
        {
            if (!m_CommandReceiver.HasCommand(command.CommandType))
            {
                Log.Error("Can no find Command");
                return CommandReplyType.NO;
            }

            return m_CommandReceiver.ExecuteCommand(command);
        }

        public bool CheckActorState(ActorStateType type)
        {
            bool flag;
            m_ActorStates.TryGetValue(type, out flag);
            return flag;
        }

        public void AddHp(int hp, bool showFlyword)
        {
            if (IsDead)
            {
                return;
            }
            int newHp = Attrbute.GetValue(AttributeType.Hp);
            if (newHp + hp > Attrbute.GetValue(AttributeType.MaxHp))
            {
                newHp = Attrbute.GetValue(AttributeType.MaxHp);
            }
            else
            {
                newHp += hp;
            }
            Attrbute.UpdateValue(AttributeType.Hp, newHp);
            if (showFlyword)
            {
                ShowFlyword(FlyWordType.HpHeal, hp);
            }
            this.UpdateHealth();
        }

        public void AddMp(int mp, bool showFlyword)
        {
            if (IsDead)
            {
                return;
            }
            int newMp = Attrbute.GetValue(AttributeType.Mp);
            if (newMp + mp > Attrbute.GetValue(AttributeType.MaxMp))
            {
                newMp = Attrbute.GetValue(AttributeType.MaxMp);
            }
            else
            {
                newMp += mp;
            }
            Attrbute.UpdateValue(AttributeType.Mp, newMp);
            if (showFlyword)
            {
                ShowFlyword(FlyWordType.MpHeal, mp);
            }
            this.UpdatePower();
        }

        public bool UseHp(int use)
        {
            int hp = Attrbute.GetValue(AttributeType.Hp);
            if (hp > use)
            {
                Attrbute.UpdateValue(AttributeType.Hp, hp - use);
                UpdatePower();
                return true;
            }

            return false;
        }

        public bool UseMp(int use)
        {
            int mp = Attrbute.GetValue(AttributeType.Mp);
            if (mp > use)
            {
                Attrbute.UpdateValue(AttributeType.Mp, mp - use);
                UpdatePower();
                return true;
            }

            return false;
        }

        public bool IsFullHp()
        {
            int hp = Attrbute.GetValue(AttributeType.Hp);
            int maxHp = Attrbute.GetValue(AttributeType.MaxHp);
            return hp >= maxHp;
        }

        public bool IsFullMp()
        {
            int mp = Attrbute.GetValue(AttributeType.Mp);
            int maxMp = Attrbute.GetValue(AttributeType.MaxMp);
            return mp >= maxMp;
        }

        public List<ActorBase> GetActorsByAffectType(AffectType type)
        {
            switch (type)
            {
                case AffectType.Ally:
                    return GetAllAlly();
                case AffectType.Host:
                    if (m_Host == null)
                    {
                        return null;
                    }
                    else
                    {
                        return new List<ActorBase>() { m_Host };
                    }
                case AffectType.Enem:
                    return GetAllEnemy();
                case AffectType.Boss:
                    List<ActorBase> list = new List<ActorBase>();
                    List<ActorBase> enemys = GetAllEnemy();
                    for (int i = 0; i < enemys.Count; i++)
                    {
                        ActorEnemy monster = enemys[i] as ActorEnemy;
                        if (monster != null && monster.ActorType == ActorType.Monster)
                        {
                            if (monster.IsBoss())
                            {
                                list.Add(monster);
                            }
                        }
                    }
                    return list;
                case AffectType.Self:
                    return new List<ActorBase>() { this };
                case AffectType.Each:
                    return LevelData.AllActors;
                default:
                    return new List<ActorBase>();
            }
        }



        protected bool IsEnemy(ActorBase actor)
        {
            if (actor == null)
            {
                Log.Error("Actor is null.");
                return false;
            }

            return GetCamp(actor) == BattleCampType.Enemy;
        }

        protected bool IsAlly(ActorBase actor)
        {
            if (actor == null)
            {
                Log.Error("Actor is null.");
                return false;
            }

            return GetCamp(actor) == BattleCampType.Ally;
        }

        protected List<ActorBase> GetAllEnemy()
        {
            m_Enemys.Clear();
            FindActorsByCamp(BattleCampType.Enemy, ref m_Enemys, true);
            return m_Enemys;
        }

        protected List<ActorBase> GetAllAlly()
        {
            m_Allys.Clear();
            FindActorsByCamp(BattleCampType.Ally,ref m_Allys);
            return m_Allys;
        }

        protected void FindActorsByCamp(BattleCampType actorCamp, ref List<ActorBase> list, bool ignoreStealth = false)
        {
            for (int i = 0; i < LevelData.AllActors.Count; i++)
            {
                ActorBase actor = LevelData.AllActors[i];
                if (actor.Camp == actorCamp && actor.IsDead == false)
                {
                    if (ignoreStealth == false)
                    {
                        list.Add(actor);
                    }
                    else
                    {
                        if (actor.GetActorState(ActorStateType.IsStealth) == false)
                        {
                            list.Add(actor);
                        }
                    }
                }
            }
        }

        protected BattleCampType GetCamp(ActorBase actor)
        {
            if (actor == null)
            {
                Log.Error("Actor is null.");
                return BattleCampType.None;
            }

            return actor.Camp;
        }

        protected ActorBase GetNearestEnemy(float radius = 100)
        {
            List<ActorBase> actors = GetAllEnemy();
            ActorBase nearest = null;
            float min = radius;
            for (int i = 0; i < actors.Count; i++)
            {
                float dist = GlobalTools.GetHorizontalDistance(actors[i].CachedTransform.position, this.CachedTransform.position);
                if (dist < min)
                {
                    min = dist;
                    nearest = actors[i];
                }
            }
            return nearest;
        }

        protected void LookAtEnemy()
        {
            if (m_Target == null || m_Target.IsDead || !IsEnemy(m_Target) || m_Target.CheckActorState(ActorStateType.IsStealth))
            {
                m_Target = null;
            }

            ActorBase enemy = GetNearestEnemy(m_ActorAI.WaringDist);
            this.SetTarget(enemy);
            if (m_Target != null)
            {
                CachedTransform.LookAt(new Vector3(m_Target.Pos.x, Pos.y, m_Target.Pos.z));
            }
        }

        protected void CreateBoard()
        {
            //TODO 创建头上ui
        }

        protected void RemoveBoard()
        {
            //TODO 移除头上ui
        }

        protected void RemoveEffect()
        {
            //TODO 移除特效
        }

        protected void ShowFlyword(FlyWordType type, int value)
        {
            if (IsDead)
            {
                return;
            }

            //TODO:显示飞字
        }

        protected void ShowWarning(string localKey)
        {
            if (m_ActorAI.AIMode == AIModeType.Hand)
            {
                //TODO:显示警告
            }
        }

        protected virtual void InitAttribute(bool init = false)
        {
            m_BaseAttribute = new ActorAttribute();
            m_CurAttribute = new ActorAttribute();
            Attrbute.UpdateValue(AttributeType.Hp, m_ActorData.LHP);
            Attrbute.UpdateValue(AttributeType.MaxHp, m_ActorData.LHP);
            Attrbute.UpdateValue(AttributeType.Attack, m_ActorData.ATK);
            Attrbute.UpdateValue(AttributeType.Defense, m_ActorData.DEF);
            Attrbute.UpdateValue(AttributeType.Crit, m_ActorData.CRI);
            Attrbute.UpdateValue(AttributeType.CritDamage, m_ActorData.BUR);
            Attrbute.UpdateValue(AttributeType.Mp, m_ActorData.LMP);
            Attrbute.UpdateValue(AttributeType.MaxMp, m_ActorData.LMP);
            Attrbute.UpdateValue(AttributeType.SuckBlood, m_ActorData.VAM);
            Attrbute.UpdateValue(AttributeType.Hit, m_ActorData.HIT);
            Attrbute.UpdateValue(AttributeType.Dodge, m_ActorData.DOG);
            Attrbute.UpdateValue(AttributeType.Absorb, m_ActorData.BAF);
            UpdateCurAttribute(init);
        }

        protected void InitFeature()
        {
            char[] flagArray = m_ActorData.AIFeature.ToCharArray();
            for (int i = 0; i < flagArray.Length; i++)
            {
                AIFeatureType type = (AIFeatureType)i;
                bool flag = int.Parse(flagArray[i].ToString()) == 1;
                m_AIFeatures[type] = flag;
            }
        }

        protected void InitLayer()
        {
            switch (ActorType)
            {
                case ActorType.Player:
                    GlobalTools.SetLayer(EntityGo, Constant.Layer.PlayerId);
                    break;
                case ActorType.Monster:
                    GlobalTools.SetLayer(EntityGo, Constant.Layer.MonsterId);
                    break;
                case ActorType.Npc:
                    GlobalTools.SetLayer(EntityGo, Constant.Layer.NpcId);
                    break;
                case ActorType.Pet:
                    GlobalTools.SetLayer(EntityGo, Constant.Layer.PetId);
                    break;
                case ActorType.Mount:
                    GlobalTools.SetLayer(EntityGo, Constant.Layer.MountId);
                    break;
                case ActorType.Partner:
                    GlobalTools.SetLayer(EntityGo, Constant.Layer.PartnerId);
                    break;
            }
        }

        protected void InitState()
        {
            this.m_AIFeatures[AIFeatureType.CanMove] = true;
            this.m_AIFeatures[AIFeatureType.CanTurn] = true;

            this.m_ActorStates[ActorStateType.IsRide] = false;
            this.m_ActorStates[ActorStateType.IsSilent] = false;
            this.m_ActorStates[ActorStateType.IsDivine] = false;
            this.m_ActorStates[ActorStateType.IsStory] = false;
            this.m_ActorStates[ActorStateType.IsTask] = false;
            this.m_ActorStates[ActorStateType.IsAutoToMove] = false;
            this.m_ActorStates[ActorStateType.IsStealth] = false;

            this.ApplyAnimator(true);
        }

        protected void InitAnim()
        {
            if (m_Animator == null)
            {
                Log.Error("Animator is null.");
                return;
            }

            m_Animator.enabled = true;
            m_Animator.applyRootMotion = true;
            m_Animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            m_AnimController = new AnimController(m_Animator);
        }

        protected void InitFsm()
        {
            FsmState<ActorBase>[] states =
            {
                new ActorEmptyFsm(),
                new ActorIdleFsm(),
                new ActorRunFsm(), 
                new ActorWalkFsm(), 
                new ActorTurnFsm(), 
                new ActorDeadFsm(), 
                new ActorSkillFsm(),             
                new ActorBeatFlyFsm(), 
                new ActorBeatBackFsm(), 
                new ActorBeatDownFsm(), 
                new ActorWoundFsm(), 
                new ActorStunFsm(), 
                new ActorFrostFsm(), 
                new ActorFixBodyFsm(), 
                new ActorFearFsm(), 
                new ActorVariationFsm(),
                new ActorJumpFsm(), 
                new ActorRebornFsm(), 
                new ActorCollectMineFsm(), 
                new ActorInteractiveFsm(), 
            };
            m_ActorFsm = GameEntry.Fsm.CreateFsm(EntityId.ToString(), this, states);
            m_ActorFsm.Start<ActorIdleFsm>();
        }

        protected void InitAi()
        {
            m_ActorPathFinding = new AIPathFinding(this);
            float atkDist = m_ActorData.AiAtkDist;
            float followDist = m_ActorData.AiFollowDist;
            float waringDist = m_ActorData.AiWaringDist;
            m_ActorAI = new ActorFsmAI(this, AIModeType.Hand, atkDist, followDist, waringDist);
            m_ActorAI.Start();
        }

        protected void InitCommands()
        {
            m_CommandReceiver = new CommandReceiver();
            m_CommandReceiver.AddCommand<IdleCommand>(CommandType.Idle, CheckIdle);
            m_CommandReceiver.AddCommand<AutoMoveCommand>(CommandType.Runto, CheckRunTo);
            m_CommandReceiver.AddCommand<UseSkillCommand>(CommandType.Useskill, CheckUseSkill);
            m_CommandReceiver.AddCommand<DeadCommand>(CommandType.Dead, CheckDead);
            m_CommandReceiver.AddCommand<TurnToCommand>(CommandType.TurnTo, CheckTurnTo);
            m_CommandReceiver.AddCommand<MoveCommand>(CommandType.Moveto, CheckMoveTo);
            m_CommandReceiver.AddCommand<TalkCommand>(CommandType.Talk, CheckTalk);
            m_CommandReceiver.AddCommand<FrostCommand>(CommandType.Frost, CheckFrost);
            m_CommandReceiver.AddCommand<StunCommand>(CommandType.Stun, CheckStun);
            m_CommandReceiver.AddCommand<PalsyCommand>(CommandType.Palsy, CheckPalsy);
            m_CommandReceiver.AddCommand<SleepCommand>(CommandType.Sleep, CheckSleep);
            m_CommandReceiver.AddCommand<BlindCommand>(CommandType.Blind, CheckBlind);
            m_CommandReceiver.AddCommand<FearCommand>(CommandType.Fear, CheckFear);
            m_CommandReceiver.AddCommand<FixBodyCommand>(CommandType.Fixbody, CheckFixBody);
            m_CommandReceiver.AddCommand<WoundCommand>(CommandType.Wound, CheckWound);
            m_CommandReceiver.AddCommand<BeatDownCommand>(CommandType.Beatdown, CheckBeatDown);
            m_CommandReceiver.AddCommand<BeatBackCommand>(CommandType.Beatback, CheckBeatBack);
            m_CommandReceiver.AddCommand<BeatFlyCommand>(CommandType.Beatfly, CheckBeatFly);
            m_CommandReceiver.AddCommand<FlyCommand>(CommandType.Fly, CheckFly);
            m_CommandReceiver.AddCommand<HookCommand>(CommandType.Hook, CheckHook);
            m_CommandReceiver.AddCommand<GrabCommand>(CommandType.Grab, CheckGrab);
            m_CommandReceiver.AddCommand<VariationCommand>(CommandType.Variation, CheckVariation);
            m_CommandReceiver.AddCommand<RideCommand>(CommandType.Ride, CheckRide);
            m_CommandReceiver.AddCommand<JumpCommand>(CommandType.Jump, CheckJump);
            m_CommandReceiver.AddCommand<StealthCommand>(CommandType.Stealth, CheckStealth);
            m_CommandReceiver.AddCommand<RebornCommand>(CommandType.Reborn, CheckReborn);
            m_CommandReceiver.AddCommand<CollectMineCommand>(CommandType.Mine, CheckMine);
            m_CommandReceiver.AddCommand<InteractiveCommand>(CommandType.Interactive, CheckInteractive);
        }

    }
}