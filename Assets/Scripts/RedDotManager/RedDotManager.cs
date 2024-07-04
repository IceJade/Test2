﻿using UnityEngine;
using System.Collections.Generic;

/********************************************************************************
* @说明: 红点管理系统
* @作者：zhoumingfeng
* @版本号：V1.00
* @简单用法如下:
  1.注册红点;
    GameEntry.DataNode.RedDotManager.RegisterObject("Social", objRedDot1);
    GameEntry.DataNode.RedDotManager.RegisterObject("Social/Union", objRedDot2);
    GameEntry.DataNode.RedDotManager.RegisterObject("Social/Union/Star", objRedDot3);

  2.显示红点;
    GameEntry.DataNode.RedDotManager.Set("Social/Union/Star", true);
    这一行代码执行之后, 上方关联的3个红点都将显示;

  3.隐藏红点;
    GameEntry.DataNode.RedDotManager.Set("Social/Union/Star", false);
    这一行代码执行之后, 上方关联的3个红点都将隐藏;

  4.多节点显隐;
    GameEntry.DataNode.RedDotManager.Set("Social/Union", true);
    GameEntry.DataNode.RedDotManager.Set("Social/Union/Star", true);
    // code do something, etc....
    GameEntry.DataNode.RedDotManager.Set("Social/Union/Star", false);
    上面这段代码执行后(前两行调用代码顺序随意)，前两个红点依然显示，最后一个不显示;
********************************************************************************/

namespace Tetris
{
    public class RedDotManager
    {
        public const string DEFAULT_CALLER = "__DEFAULT_CALLER__";

        // 通知节点表;
        private Dictionary<int, UINotificationNode> m_nodes = new Dictionary<int, UINotificationNode>();

        // 节点hash与红点操作对应表;
        private Dictionary<int, UINotificationOPRedDot> m_opMap = new Dictionary<int, UINotificationOPRedDot>();

        private static long s_displaySerialNum = 0;

        private PathParser m_PathParser = new PathParser();

        #region 对外接口

        /// <summary>
        /// 设置红点显隐;
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="visible">true:显示 false:隐藏</param>
        /// <param name="caller">操作者</param>
        public void Set(string path, bool visible, string caller = RedDotManager.DEFAULT_CALLER)
        {
            if (!GameUtils.IsValidString(path))
                return;

            if (visible)
            {
                this.IncreaseNotificationCount(path, caller);

            }
            else
            {
                this.DecreaseNotificationCount(path, caller);
            }
        }

        /// <summary>
        /// 清除红点计数;
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="clearChildern">是否清除子路径计数</param>
        public void Clear(string path, bool clearChildern = false)
        {
            this.ClearNotificationCount(path, clearChildern);
        }

        /// <summary>
        /// 消除红点显示，但不清除计数，下次Set true后恢复（路径）
        /// 会隐藏该节点以及所有父节点的红点（NotifyParent配置为2）
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        public void Erase(string path)
        {
            this.EraseNotificationDisplay(path);
        }

        /// <summary>
        /// 红点是否可见;
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>true:可见 false:不可见</returns>
        public bool IsVisible(string path)
        {
            UINotificationNode node = this.GetNode(path);
            return node != null ? node.IsRedDotVisible : false;
        }

        /// <summary>
        /// 设置红点始终隐藏（无论计数多少都不可见）（路径）;
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="alwaysHide">true:始终可见 false:始终隐藏</param>
        public void SetAlwaysHide(string path, bool alwaysHide)
        {
            this.SetNodeAlwaysHide(path, alwaysHide);
        }


        /// <summary>
        /// 通过路径注册红点对象;
        /// 如果之前注册过obj的话，会改用新路径，不会出现添加2次的情况，所以添加前可以不Remove
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="objRedDot">红点对象</param>
        public void RegisterObject(string path, GameObject objRedDot)
        {
            if (null == objRedDot)
                return;

            if (!GameUtils.IsValidString(path))
                return;

            int nodeHash = RedDotManager.GetNodeHash(path);
            int instanceID = objRedDot.GetInstanceID();
            UINotificationOPRedDot op = null;
            if (this.m_opMap.TryGetValue(instanceID, out op))
            {
                if (op.nodeHash != nodeHash)
                {
                    m_opMap.Remove(instanceID);
                    this.RemoveNotificationOPByNodeHash(op.nodeHash, op);
                    op.nodeHash = nodeHash;
                    op.path = path;
                }
                else
                {
                    return;
                }
            }
            else
            {
                op = new UINotificationOPRedDot(nodeHash, objRedDot);
                op.path = path;
            }

            m_opMap.Add(instanceID, op);
            this.AddNotificationOPByNodeHash(nodeHash, op);
        }

        /// <summary>
        /// 移除路径中的所有红点;
        /// </summary>
        /// <param name="path">路径</param>
        public void RemoveObjects(string path)
        {
            if (!GameUtils.IsValidString(path))
                return;

            int nodeHash = RedDotManager.GetNodeHash(path);
            UINotificationOPRedDot removedOP = null;
            var e = m_opMap.GetEnumerator();
            while (e.MoveNext())
            {
                if (e.Current.Value.nodeHash == nodeHash)
                {
                    this.RemoveNotificationOPByNodeHash(nodeHash, e.Current.Value);
                    if (removedOP == null)
                    {
                        removedOP = e.Current.Value;
                    }
                    else
                    {
                        ListLinker.AddNext(removedOP, e.Current.Value);
                    }
                }
            }
            e.Dispose();

            ListLinker.Iterator iter = new ListLinker.Iterator(removedOP);
            while (iter.MoveNext())
            {
                m_opMap.Remove((iter.Current as UINotificationOPRedDot).instanceID);
            }
        }

        /// <summary>
        /// 移除路径中的指定红点;
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="obj">红点对象</param>
        public void RemoveObject(string path, GameObject obj)
        {
            if (null == obj)
                return;

            if (!GameUtils.IsValidString(path))
                return;

            int nodeHash = RedDotManager.GetNodeHash(path);
            UINotificationOPRedDot op = null;
            int instanceID = obj.GetInstanceID();
            if (m_opMap.TryGetValue(instanceID, out op) && op.nodeHash == nodeHash)
            {
                m_opMap.Remove(instanceID);
                this.RemoveNotificationOPByNodeHash(op.nodeHash, op);
            }
        }

        /// <summary>
        /// 清除所有红点;
        /// </summary>
        public void ClearObjects()
        {
            UINotificationOPRedDot op = null;
            var e = m_opMap.GetEnumerator();
            while (e.MoveNext())
            {
                op = e.Current.Value as UINotificationOPRedDot;
                this.RemoveNotificationOPByNodeHash(op.nodeHash, op);
            }
            e.Dispose();
            m_opMap.Clear();
        }

        /// <summary>
        /// 更新红点状态;
        /// </summary>
        public void UpdateObjects()
        {
            UINotificationOPRedDot op = null;
            var e = m_opMap.GetEnumerator();
            while (e.MoveNext())
            {
                op = e.Current.Value as UINotificationOPRedDot;
                op.OnNotification(op.path, op.nodeHash, this.GetNotificationCountByNodeHash(op.nodeHash) > 0);
            }
            e.Dispose();
        }

        /// <summary>
        /// 重置（切换角色后调用）;
        /// </summary>
        public void Reset()
        {
            UINotificationNode node = null;
            var e = m_nodes.GetEnumerator();
            while (e.MoveNext())
            {
                node = e.Current.Value;
                if (node.IsRoot())
                {
                    node.ClearNotificationCount(true, false);
                }
                node.Reset();
            }
            e.Dispose();
            m_nodes.Clear();
        }

        #endregion

        #region 内部接口

        /// <summary>
        /// 添加节点。注意：一旦添加，不能移除，不要添加一次性的节点。
        /// </summary>
        /// <param name="path"></param>
        /// <param name="notifyParent"></param>
        /// <returns></returns>
        private UINotificationNode AddNode(string path, bool notifyParent)
        {
            return AddNodeInternal(path, notifyParent ? 1 : 0);
        }

        /// <summary>
        /// 获取节点
        /// </summary>
        /// <param name="path"></param>
        /// <param name="autoCreate"></param>
        /// <returns></returns>
        private UINotificationNode GetNode(string path, bool autoCreate = false)
        {
            UINotificationNode node = GetNode(GetNodeHash(path));
            if (node == null && autoCreate)
            {
                node = AddNodeInternal(path, -1);
            }
            return node;
        }

        /// <summary>
        /// 获取节点（+1重载）
        /// </summary>
        /// <param name="nodeHash"></param>
        /// <returns></returns>
        private UINotificationNode GetNode(int nodeHash)
        {
            UINotificationNode node = null;
            if (m_nodes.TryGetValue(nodeHash, out node))
            {
                return node;
            }
            return null;
        }

        /// <summary>
        /// 显示（隐藏）通知红点，如果只有一个地方控制红点，用此函数来控制红点显隐。
        /// 注意：不存在的路径节点将自动创建，不要写错路径。
        /// </summary>
        /// <param name="path"></param>
        /// <param name="show"></param>
        private void ShowNotification(string path, bool show)
        {
            UINotificationNode node = GetNode(path, true);
            if (node == null)
            {
                return;
            }

            if (show)
            {
                node.IncreaseNotificationCount(DEFAULT_CALLER);
            }
            else
            {
                node.ClearNotificationCount(false);
            }
        }

        /// <summary>
        /// 设置红点始终隐藏（无论计数多少都不可见）
        /// </summary>
        /// <param name="path"></param>
        /// <param name="alwaysHide"></param>
        private void SetNodeAlwaysHide(string path, bool alwaysHide)
        {
            if (!GameUtils.IsValidString(path))
                return;

            UINotificationNode node = GetNode(path, true);
            if (node != null)
            {
                node.SetAlwaysHide(alwaysHide);
            }
        }

        /// <summary>
        /// 增加通知的计数，如果需要多个地方控制同一个红点，使用此函数。
        /// caller传不为空的字符串，表示调用者，便于调用次数统计。
        /// 注意：不存在的路径节点将自动创建，不要写错路径。
        /// </summary>
        /// <param name="path"></param>
        /// <param name="caller"></param>
        private void IncreaseNotificationCount(string path, string caller)
        {
            caller = GetCallerName(caller);
            UINotificationNode node = GetNode(path, true);
            if (node != null)
            {
                node.IncreaseNotificationCount(caller);
            }
        }

        /// <summary>
        /// 减少通知的计数
        /// </summary>
        /// <param name="path"></param>
        /// <param name="caller"></param>
        private void DecreaseNotificationCount(string path, string caller)
        {
            caller = GetCallerName(caller);
            UINotificationNode node = GetNode(path);
            if (node != null)
            {
                node.DecreaseNotificationCount(caller);
            }
        }

        /// <summary>
        /// 清除所有通知计数（包括其他调用的地方，慎用）
        /// </summary>
        /// <param name="path"></param>
        /// <param name="clearChildren"></param>
        private void ClearNotificationCount(string path, bool clearChildren)
        {
            UINotificationNode node = GetNode(path);
            if (node != null)
            {
                node.ClearNotificationCount(clearChildren);
            }
        }

        /// <summary>
        /// 消除红点显示，但不清除计数，下次Set true后恢复
        /// </summary>
        /// <param name="path"></param>
        private void EraseNotificationDisplay(string path)
        {
            UINotificationNode node = GetNode(path);
            if (node != null)
            {
                node.EraseDisplay();
            }
        }

        /// <summary>
        /// 取得当前通知计数，大于0则表示红点显示
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private int GetNotificationCount(string path)
        {
            UINotificationNode node = GetNode(path);
            if (node != null)
            {
                return node.NotificationCount;
            }
            return 0;
        }

        /// <summary>
        /// 通过节点hash值取得当前通知计数;
        /// </summary>
        /// <param name="nodeHash"></param>
        /// <returns></returns>
        private int GetNotificationCountByNodeHash(int nodeHash)
        {
            UINotificationNode node = GetNode(nodeHash);
            if (node != null)
            {
                return node.NotificationCount;
            }
            return 0;
        }

        /// <summary>
        /// 添加红点通知响应;
        /// </summary>
        /// <param name="path"></param>
        /// <param name="op"></param>
        /// <returns></returns>
        private bool AddNotificationOP(string path, UINotificationOPRedDot op)
        {
            return AddNotificationOPByNodeHash(GetNodeHash(path), op);
        }

        /// <summary>
        /// 添加红点通知响应;
        /// </summary>
        /// <param name="nodeHash"></param>
        /// <param name="op"></param>
        /// <returns></returns>
        private bool AddNotificationOPByNodeHash(int nodeHash, UINotificationOPRedDot op)
        {
            bool ret = false;
            UINotificationOPRedDot root = null;
            if (m_opMap.TryGetValue(nodeHash, out root))
            {
                ret = ListLinker.AddNext(root, op) != null;
            }
            else
            {
                m_opMap.Add(nodeHash, op);
                ret = true;
            }
            if (ret)
            {
                UINotificationNode node = GetNode(nodeHash);
                if (node != null)
                {
                    op.OnNotification(node.Path, node.NodeHash, node.IsRedDotVisible);
                }
                else
                {
                    op.OnNotification(null, 0, false);
                }
            }
            return ret;
        }

        /// <summary>
        /// 移除红点通知响应;
        /// </summary>
        /// <param name="path"></param>
        /// <param name="op"></param>
        private bool RemoveNotificationOP(string path, UINotificationOPRedDot op)
        {
            return RemoveNotificationOPByNodeHash(GetNodeHash(path), op);
        }

        /// <summary>
        /// 移除红点通知响应;
        /// </summary>
        /// <param name="nodeHash"></param>
        /// <param name="op"></param>
        private bool RemoveNotificationOPByNodeHash(int nodeHash, UINotificationOPRedDot op)
        {
            UINotificationOPRedDot root = null;
            if (m_opMap.TryGetValue(nodeHash, out root))
            {
                UINotificationOPRedDot nextOP = ListLinker.Remove(op) as UINotificationOPRedDot;
                if (nextOP == null || root == op)
                {
                    if (nextOP == null)
                    {
                        m_opMap.Remove(nodeHash);
                    }
                    else
                    {
                        m_opMap[nodeHash] = nextOP;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取红点通知响应;
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private UINotificationOPRedDot GetNotificationOP(int nodeHash)
        {
            UINotificationOPRedDot root = null;
            if (m_opMap.TryGetValue(nodeHash, out root))
            {
                return root;
            }
            return null;
        }

        // 获取节点hash
        public static int GetNodeHash(string path)
        {
            return Animator.StringToHash(path);
        }

        // 红点显示序列号，当前序列号小于显示序列号时才显示
        public static long GetDisplaySerialNum()
        {
            return ++s_displaySerialNum;
        }

        // UINotificationNode回调
        public void OnNotification(UINotificationNode node, bool show)
        {
            if (node == null) return;
            UINotificationOPRedDot root = null;
            if (m_opMap.TryGetValue(node.NodeHash, out root))
            {
                ListLinker.Iterator iter = new ListLinker.Iterator(root);
                IUINotificationOP op;
                while (iter.MoveNext())
                {
                    op = (iter.Current as IUINotificationOP);
                    if (op != null)
                    {
                        op.OnNotification(node.Path, node.NodeHash, show);
                    }
                    else
                    {
                        iter.RemoveCurrent();
                    }
                }
            }
        }

        private UINotificationNode AddNodeInternal(string path, int notifyParent)
        {
            UINotificationNode node = GetNode(path);
            if (node != null)
            {
                if (notifyParent >= 0)
                {
                    node.NotifyParent = notifyParent;
                }
                return node;
            }

            int pos = path.LastIndexOf('/');
            if (pos == 0)
            {
                return null;
            }

            node = new UINotificationNode(this, path);
            if (notifyParent >= 0)
            {
                node.NotifyParent = notifyParent;
            }

            if (pos > 0)
            {
                UINotificationNode parent = AddNodeInternal(m_PathParser.GetParent(path), -1);
                if (parent != null)
                {
                    parent.AddChild(node);
                }
                else
                {
                    return null;
                }
            }

            m_nodes.Add(node.NodeHash, node);

            return node;
        }

        private static string GetCallerName(string caller)
        {
            return !string.IsNullOrEmpty(caller) ? caller : DEFAULT_CALLER;
        }

        #endregion
    }
}
