using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UITKit.Components;

/// <summary>
/// 横並びタブ。子は均等幅 (flex-grow:1)。SetActive で見た目切替、OnTabClicked で click 通知。
/// </summary>
public class UITTabStrip : VisualElement
{
    public event Action<int> OnTabClicked;

    private readonly System.Collections.Generic.List<VisualElement> m_tabs = new();
    private int m_active = -1;

    public void Setup(string[] labels, Font font = null)
    {
        Clear();
        m_tabs.Clear();
        style.flexDirection = FlexDirection.Row;
        style.height = 26;

        for (int i = 0; i < labels.Length; i++)
        {
            int captured = i;
            var tab = new VisualElement();
            tab.style.flexGrow = 1;
            tab.style.flexBasis = 0;
            tab.style.marginRight = i < labels.Length - 1 ? 4 : 0;
            tab.style.justifyContent = Justify.Center;
            tab.style.alignItems = Align.Center;
            tab.style.borderTopLeftRadius = UITTheme.Tab.Radius;
            tab.style.borderTopRightRadius = UITTheme.Tab.Radius;
            tab.style.borderBottomLeftRadius = UITTheme.Tab.Radius;
            tab.style.borderBottomRightRadius = UITTheme.Tab.Radius;
            UITStyles.ApplyTabInactive(tab);

            var label = UITFactory.CreateLabel(labels[i], 11, UITTheme.Text.Primary, font, TextAnchor.MiddleCenter);
            tab.Add(label);
            tab.RegisterCallback<ClickEvent>(_ => OnTabClicked?.Invoke(captured));

            Add(tab);
            m_tabs.Add(tab);
        }
    }

    public void SetActive(int index)
    {
        m_active = index;
        for (int i = 0; i < m_tabs.Count; i++)
        {
            if (i == index) UITStyles.ApplyTabActive(m_tabs[i]);
            else UITStyles.ApplyTabInactive(m_tabs[i]);
        }
    }
}
