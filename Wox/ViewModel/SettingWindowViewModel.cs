﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PropertyChanged;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using static System.String;

namespace Wox.ViewModel
{
    [ImplementPropertyChanged]
    public class SettingWindowViewModel
    {
        public Settings Settings { get; set; }

        private readonly JsonStrorage<Settings> _storage;
        private readonly Dictionary<ISettingProvider, Control> _featureControls = new Dictionary<ISettingProvider, Control>();

        #region general
        public List<Language> Languages => InternationalizationManager.Instance.LoadAvailableLanguages();
        public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);
        #endregion
        #region plugin
        public PluginViewModel SelectedPlugin { get; set; }
        public IList<PluginViewModel> PluginViewModels
        {
            get
            {
                var plugins = PluginManager.AllPlugins;
                var settings = Settings.PluginSettings.Plugins;
                plugins.Sort((a, b) =>
                {
                    var d1 = settings[a.Metadata.ID].Disabled;
                    var d2 = settings[b.Metadata.ID].Disabled;
                    if (d1 == d2)
                    {
                        return Compare(a.Metadata.Name, b.Metadata.Name, StringComparison.CurrentCulture);
                    }
                    else
                    {
                        return d1.CompareTo(d2);
                    }
                });

                var metadatas = plugins.Select(p => new PluginViewModel
                {
                    PluginPair = p,
                    Metadata = p.Metadata,
                    Plugin = p.Plugin
                }).ToList();
                return metadatas;
            }
        }

        public Control SettingProvider
        {
            get
            {
                var settingProvider = SelectedPlugin.Plugin as ISettingProvider;
                if (settingProvider != null)
                {
                    Control control;
                    if (!_featureControls.TryGetValue(settingProvider, out control))
                    {
                        var multipleActionKeywordsProvider = settingProvider as IMultipleActionKeywords;
                        if (multipleActionKeywordsProvider != null)
                        {
                            multipleActionKeywordsProvider.ActionKeywordsChanged += (o, e) =>
                            {
                                // update in-memory data
                                PluginManager.UpdateActionKeywordForPlugin(SelectedPlugin.PluginPair, e.OldActionKeyword,
                                    e.NewActionKeyword);
                                // update persistant data
                                Settings.PluginSettings.UpdateActionKeyword(SelectedPlugin.Metadata);

                                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("succeed"));
                            };
                        }

                        _featureControls.Add(settingProvider, control = settingProvider.CreateSettingPanel());
                    }
                    control.HorizontalAlignment = HorizontalAlignment.Stretch;
                    control.VerticalAlignment = VerticalAlignment.Stretch;
                    return control;
                }
                else
                {
                    return new Control();
                }
            }
        }
        #endregion
        #region theme

        public string SelectedTheme
        {
            get
            {
                return Settings.Theme;
            }
            set
            {
                Settings.Theme = value;
                ThemeManager.Instance.ChangeTheme(value);
            }
        }
        public List<string> Themes => ThemeManager.Instance.LoadAvailableThemes().Select(Path.GetFileNameWithoutExtension).ToList();

        public Brush PreviewBackground
        {
            get
            {
                var wallpaper = WallpaperPathRetrieval.GetWallpaperPath();
                if (wallpaper != null && File.Exists(wallpaper))
                {
                    var memStream = new MemoryStream(File.ReadAllBytes(wallpaper));
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = memStream;
                    bitmap.EndInit();
                    var brush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                    return brush;
                }
                else
                {
                    var wallpaperColor = WallpaperPathRetrieval.GetWallpaperColor();
                    var brush = new SolidColorBrush(wallpaperColor);
                    return brush;
                }
            }
        }

        public ResultsViewModel PreviewResults
        {
            get
            {

                const string image = "app.png";
                const string theme = "http://www.getwox.com/theme/builder";
                const string plugin = "http://www.getwox.com/plugin";
                List<Result> results = new List<Result>
                {
                    new Result
                    {
                        Title = "WoX is a launcher for Windows that simply works.",
                        SubTitle = "You can call it Windows omni-eXecutor if you want a long name.",
                        IcoPath = image,
                    },
                    new Result
                    {
                        Title = "Search for everything—applications, folders, files and more.",
                        SubTitle = "Use pinyin to search for programs. (yyy / wangyiyun → 网易云音乐)",
                        IcoPath = image,
                    },
                    new Result
                    {
                        Title = "Keyword plugin search.",
                        SubTitle = "search google with g search_term.",
                        IcoPath = image,
                    },
                    new Result
                    {
                        Title = "Build custom themes at: ",
                        SubTitle = theme,
                    },
                    new Result
                    {
                        Title = "Install plugins from: ",
                        SubTitle = plugin,
                        IcoPath = image,
                    },
                    new Result
                    {
                        Title = $"Open Source: {Infrastructure.Constant.Github}",
                        SubTitle = "Please star it!",
                        IcoPath = image,
                    }
                };
                var vm = new ResultsViewModel(6);
                vm.AddResults(results, "PREVIEW");
                return vm;
            }
        }

        public FontFamily SelectedQueryBoxFont
        {
            get
            {
                if (Fonts.SystemFontFamilies.Count(o =>
                    o.FamilyNames.Values != null &&
                    o.FamilyNames.Values.Contains(Settings.QueryBoxFont)) > 0)
                {
                    var font = new FontFamily(Settings.QueryBoxFont);
                    return font;
                }
                else
                {
                    var font = new FontFamily("Segoe UI");
                    return font;
                }
            }
            set
            {
                Settings.QueryBoxFont = value.ToString();
                ThemeManager.Instance.ChangeTheme(Settings.Theme);
            }
        }

        public FamilyTypeface SelectedQueryBoxFontFaces
        {
            get
            {
                var typeface = SyntaxSugars.CallOrRescueDefault(
                    () => SelectedQueryBoxFont.ConvertFromInvariantStringsOrNormal(
                        Settings.QueryBoxFontStyle,
                        Settings.QueryBoxFontWeight,
                        Settings.QueryBoxFontStretch
                        ));
                return typeface;
            }
            set
            {
                Settings.QueryBoxFontStretch = value.Stretch.ToString();
                Settings.QueryBoxFontWeight = value.Weight.ToString();
                Settings.QueryBoxFontStyle = value.Style.ToString();
                ThemeManager.Instance.ChangeTheme(Settings.Theme);
            }
        }

        public FontFamily SelectedResultFont
        {
            get
            {
                if (Fonts.SystemFontFamilies.Count(o =>
                    o.FamilyNames.Values != null &&
                    o.FamilyNames.Values.Contains(Settings.ResultFont)) > 0)
                {
                    var font = new FontFamily(Settings.ResultFont);
                    return font;
                }
                else
                {
                    var font = new FontFamily("Segoe UI");
                    return font;
                }
            }
            set
            {
                Settings.ResultFont = value.ToString();
                ThemeManager.Instance.ChangeTheme(Settings.Theme);
            }
        }

        public FamilyTypeface SelectedResultFontFaces
        {
            get
            {
                var typeface = SyntaxSugars.CallOrRescueDefault(
                    () => SelectedQueryBoxFont.ConvertFromInvariantStringsOrNormal(
                        Settings.ResultFontStyle,
                        Settings.ResultFontWeight,
                        Settings.ResultFontStretch
                        ));
                return typeface;
            }
            set
            {
                Settings.ResultFontStretch = value.Stretch.ToString();
                Settings.ResultFontWeight = value.Weight.ToString();
                Settings.ResultFontStyle = value.Style.ToString();
                ThemeManager.Instance.ChangeTheme(Settings.Theme);
            }
        }


        #endregion
        public SettingWindowViewModel()
        {
            _storage = new JsonStrorage<Settings>();
            Settings = _storage.Load();
        }

        //todo happlebao save
        public void Save()
        {
            _storage.Save();
        }
    }
}