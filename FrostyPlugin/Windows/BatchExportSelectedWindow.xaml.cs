using Frosty.Controls;
using Frosty.Core.Controls;
using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Frosty.Core.Windows
{
    /// <summary>
    /// Interaction logic for RenameInstanceWindow.xaml
    /// </summary>
    public partial class BatchExportSelectedWindow : FrostyDockableWindow
    {
        private AssetPath selectedPath = null;
        private IEnumerable itemsSource = null;

        public BatchExportSelectedWindow(AssetPath selectedPath, IEnumerable itemsSource)
        {
            InitializeComponent();

            this.selectedPath = selectedPath;
            this.itemsSource = itemsSource;
        }

        private void doneButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;

            FrostyFolderBrowserDialog fbd = new FrostyFolderBrowserDialog("Batch Export Location", "", "");
            if (fbd.ShowDialog())
            {
                string path = fbd.SelectedPath;
                Console.WriteLine(path);
            }

            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InstanceNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
