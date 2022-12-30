using Frosty.Controls;
using Frosty.Core.Controls;
using FrostySdk.Managers.Entries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
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
        public bool includeSubDirectories { get; set; } = false;

        public List<string> types = new List<string>()
        { 
            "RigidMeshAsset", 
            "CompositeMeshAsset" 
        };

        public BatchExportSelectedWindow(AssetPath selectedPath, IEnumerable itemsSource)
        {
            InitializeComponent();

            this.selectedPath = selectedPath;
            this.itemsSource = itemsSource;
        }

        private void doneButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            FrostyFolderBrowserDialog fbd = new FrostyFolderBrowserDialog("Batch Export Location");
            if (fbd.ShowDialog())
            {
                string path = fbd.SelectedPath;
                string fullPath = selectedPath.FullPath.Trim('/');

                var stopWatch = new Stopwatch();
                stopWatch.Start();

                List<EbxAssetEntry> entries = new List<EbxAssetEntry>();
                if (includeSubDirectories)
                {
                    foreach (AssetEntry entry in itemsSource)
                    {
                        if (entry.Path.Contains(fullPath.ToLower()))
                        {
                            if (types.Contains(entry.Type))
                            {
                                entries.Add((EbxAssetEntry)entry);
                            }
                        }
                    }
                }
                else
                {
                    foreach (AssetEntry entry in itemsSource)
                    {
                        if (entry.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (types.Contains(entry.Type))
                            {
                                entries.Add((EbxAssetEntry)entry);
                            }
                        }
                    }
                }

                int initialCount = entries.Count;

                int i = 0;
                while (entries.Count > 0 && i < entries.Count)
                {
                    // use the first instance of a given AssetDefinition to export all instances of that AssetDefinition in the array
                    AssetDefinition assetDefinition = App.PluginManager.GetAssetDefinition(entries[i].Type) ?? new AssetDefinition();
                    List<EbxAssetEntry> leftOverEntries = assetDefinition.BatchExport(entries, path, stopWatch);
                    
                    // If we somehow failed to export any entries, add 1 to i to avoid an infinite loop
                    if (leftOverEntries.Count == entries.Count)
                    {
                        i++;
                    }

                    entries = leftOverEntries;

                }

                stopWatch.Stop();

                int finalCount = entries.Count;
                int totalExported = initialCount - finalCount;

                var ts = stopWatch.Elapsed;
                string elapsedTime = $"{ts.Seconds}.{ts.Milliseconds}";

                App.Logger.Log("Successfully exported {0} assets in {1} seconds.", totalExported, elapsedTime);


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
