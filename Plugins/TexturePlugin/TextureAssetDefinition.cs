using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using System.Collections.Generic;
using System.Windows.Media;
using FrostySdk.Managers.Entries;
using System.Diagnostics;
using SharpDX.Direct3D11;
using System.IO;
using Frosty.Core.Windows;

namespace TexturePlugin
{
    public class TextureAssetDefinition : AssetDefinition
    {
        private static ImageSource imageSource = new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Assets/ImageFileType.png") as ImageSource;

        public TextureAssetDefinition()
        {
        }

        public override void GetSupportedExportTypes(List<AssetExportType> exportTypes)
        {
            exportTypes.Add(new AssetExportType("png", "Portable Network Graphics"));
            exportTypes.Add(new AssetExportType("tga", "Truevision TGA"));
            exportTypes.Add(new AssetExportType("hdr", "High Dynamic Range"));
            exportTypes.Add(new AssetExportType("dds", "Direct Draw Surface"));

            base.GetSupportedExportTypes(exportTypes);
        }

        public override ImageSource GetIcon()
        {
            return imageSource;
        }

        public override List<EbxAssetEntry> BatchExport(List<EbxAssetEntry> entries, string path, Stopwatch stopWatch)
        {
            FrostyTaskWindow.Show("Exporting Textures", "", (task) =>
            {
                TextureExporter exporter = new TextureExporter();

                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    if (entries[i].Type == "TextureAsset")
                    {

                        EbxAsset asset = App.AssetManager.GetEbx(entries[i]);
                        dynamic textureAsset = (dynamic)asset.RootObject;

                        ResAssetEntry resEntry = App.AssetManager.GetResEntry(textureAsset.Resource);
                        Texture texture = App.AssetManager.GetResAs<Texture>(resEntry);

                        // define combine user defined path with the asset's data explorer path
                        string assetPath = Path.Combine(path, entries[i].Path);
                        string assetFullPath = Path.Combine(path, entries[i].Name);
                        string assetName = Path.GetFileName(entries[i].Name);
                        System.IO.Directory.CreateDirectory(assetPath);

                        task.Update("Writing " + assetName);
                        exporter.Export(texture, assetFullPath + ".tga", "*.tga");

                        entries.RemoveAt(i);
                    }
                }

            });

            return entries;

        }


        public override bool Export(EbxAssetEntry entry, string path, string filterType)
        {
            if (!base.Export(entry, path, filterType))
            {
                if (filterType == "png" || filterType == "tga" || filterType == "hdr" || filterType == "dds")
                {
                    EbxAsset asset = App.AssetManager.GetEbx(entry);
                    dynamic textureAsset = (dynamic)asset.RootObject;

                    ResAssetEntry resEntry = App.AssetManager.GetResEntry(textureAsset.Resource);
                    Texture texture = App.AssetManager.GetResAs<Texture>(resEntry);

                    TextureExporter exporter = new TextureExporter();
                    exporter.Export(texture, path, "*." + filterType);
                    return true;
                }
            }

            return false;
        }

        public override FrostyAssetEditor GetEditor(ILogger logger)
        {
            return new FrostyTextureEditor(logger);
        }
    }
}
