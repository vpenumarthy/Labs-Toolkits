﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InRule.Authoring.Services;
using InRule.Labs.Toolkit.Shared;
using InRule.Labs.Toolkit.Shared.Model;
using InRule.Repository;

namespace InRule.Labs.Toolkit.Authoring
{
    /// <summary>
    /// Interaction logic for RuleSetByCategoryTree.xaml
    /// </summary>
    public partial class ToolkitTree : UserControl
    {
        
        public ToolkitTree(ToolkitsContainer toolkitsContainer)
        {
            InitializeComponent();
            DataContext = toolkitsContainer;

        }

        private void SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // set selected item
            ((ToolkitsContainer)DataContext).SelectedItem = e.NewValue;
        }
        
    }
}