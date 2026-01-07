using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TechParamsCalc.Controllers;
using TechParamsCalc.Factory;
using TechParamsCalc.Parameters;
using System.Collections.ObjectModel;
using System.Data;

namespace TechParamsCalc.UserControls
{
    /// <summary>
    /// Interaction logic for ParametersUC.xaml
    /// </summary>
    public partial class ParametersUC : UserControl
    {
        internal ParametersUC()
        {           
            InitializeComponent();
        }

        #region Row events
        private void CapacityGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selItem = ((DataGrid)sender).SelectedItem as Capacity;
            var percDescr = new string[] { "...", "...", "...", "...", "..."};

            if (selItem != null)
            {
                capacityUpDesc.Content = selItem.Description;
                capacityUpTagName.Content = selItem.TagName;
                capacityUpValue.Content = selItem.ValHmi;

                capacityUpDelta.Content = selItem.DeltaC;
                capacityUpCalcValue.Content = selItem.ValCalc;

                capacityUpTempTagName.Content = "...";
                capacityUpTempValue.Content = "...";

                if (selItem.Temperature != null)
                {
                    capacityUpTempTagName.Content = selItem.Temperature.TagName;
                    capacityUpTempValue.Content = selItem.Temperature.Val_R.ToString("f3");
                }

                for (int i = 0; i < selItem.PercDescription.Length; i++)
                    percDescr[i] = selItem.PercDescription[i];

                capacityDownTextBox1.Text = percDescr[0];
                capacityDownTextBox2.Text = percDescr[1];
                capacityDownTextBox3.Text = percDescr[2];
                capacityDownTextBox4.Text = percDescr[3];
                capacityDownTextBox5.Text = percDescr[4];
            }
        }

        private void DensityGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selItem = ((DataGrid)sender).SelectedItem as Density;
            var percDescr = new string[] { "...", "...", "...", "...", "..." };

            if (selItem != null)
            {
                densityUpDesc.Content = selItem.Description;
                densityUpTagName.Content = selItem.TagName;
                densityUpValue.Content = selItem.ValHmi;

                densityUpDelta.Content = selItem.DeltaD;
                densityUpCalcValue.Content = selItem.ValCalc;

                densityUpTempTagName.Content = "...";
                densityUpTempValue.Content = "...";

                if (selItem.Temperature != null)
                {
                    densityUpTempTagName.Content = selItem.Temperature.TagName;
                    densityUpTempValue.Content = selItem.Temperature.Val_R.ToString("f3");
                }

                densityUpPressTagName.Content = "...";
                densityUpPressValue.Content = "...";

                if (selItem.Pressure != null)
                {
                    if (selItem.Pressure.TagName != "PressureSample")
                    {
                        densityUpPressTagName.Content = selItem.Pressure.TagName;
                        densityUpPressValue.Content = selItem.Pressure.Val_R.ToString("f3");
                    }
                }

                for (int i = 0; i < selItem.PercDescription.Length; i++)
                    percDescr[i] = selItem.PercDescription[i];

                densityDownTextBox1.Text = percDescr[0];
                densityDownTextBox2.Text = percDescr[1];
                densityDownTextBox3.Text = percDescr[2];
                densityDownTextBox4.Text = percDescr[3];
                densityDownTextBox5.Text = percDescr[4];
            }
        }

        private void ContentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selItem = ((DataGrid)sender).SelectedItem as Content;
            var percDescr = new string[] { "...", "...", "...", "...", "..." };
            var percValue = new string[] { "...", "...", "...", "...", "..." };

            if (selItem != null)
            {
                contentUpDesc.Content = selItem.Description;
                contentUpTagName.Content = selItem.TagName;
                contentUpValue.Content = selItem.ValHmi;

                contentUpConf.Content = selItem.Conf;

                contentUpTempTagName.Content = "...";
                contentUpTempValue.Content = "...";

                if (selItem.Temperature != null)
                {
                    contentUpTempTagName.Content = selItem.Temperature.TagName;
                    contentUpTempValue.Content = selItem.Temperature.Val_R.ToString("f3");
                }

                contentUpPressTagName.Content = "...";
                contentUpPressValue.Content = "...";

                if (selItem.Pressure != null)
                {
                    if (selItem.Pressure.TagName != "PressureSample")
                    {
                        contentUpPressTagName.Content = selItem.Pressure.TagName;
                        contentUpPressValue.Content = selItem.Pressure.Val_R.ToString("f3");
                    }
                }

                for (int i = 0; i < selItem.PercDescription.Length; i++)
                {
                    percDescr[i] = selItem.PercDescription[i];
                    percValue[i] = selItem.ValCalc[i].ToString();
                }

                contentDownTextBox1.Text = percDescr[0];
                contentDownTextBox2.Text = percDescr[1];
                contentDownTextBox3.Text = percDescr[2];
                contentDownTextBox4.Text = percDescr[3];
                contentDownTextBox5.Text = percDescr[4];

                contentDownValue1.Content = percValue[0];
                contentDownValue2.Content = percValue[1];
                contentDownValue3.Content = percValue[2];
                contentDownValue4.Content = percValue[3];
                contentDownValue5.Content = percValue[4];

                contentDownValue1.Background = percValue[0] != "-1" ? Brushes.Green : (Brush)(SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");
                contentDownValue2.Background = percValue[1] != "-1" ? Brushes.Green : (Brush)(SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");
                contentDownValue3.Background = percValue[2] != "-1" ? Brushes.Green : (Brush)(SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");
                contentDownValue4.Background = percValue[3] != "-1" ? Brushes.Green : (Brush)(SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");
                contentDownValue5.Background = percValue[4] != "-1" ? Brushes.Green : (Brush)(SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");
            }
        }

        private void TankGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selItem = ((DataGrid)sender).SelectedItem as LevelTank;
            var percDescr = new string[] { "...", "...", "...", "...", "..." };
            var percValue = new string[] { "...", "...", "...", "...", "..." };

            if (selItem != null)
            {
                tankUpTagName.Content  = selItem.TagName;
                tankUpId.Content       = $"{selItem.Id} ({selItem.Tank.id}) - {selItem.Tank.tankDef}";

                tankUpHeigh.Content    = (selItem.DistanceB - selItem.DistanceA);
                tankUpLevel.Content    = selItem.LevelMm;
                tankUpVolume.Content   = selItem.Volume.ToString("f3");
                tankUpMass.Content     = selItem.Mass.ToString("f3");

                tankUpHeigh.Background  = (selItem.DistanceB - selItem.DistanceA) > 0 ? Brushes.Green : (Brush)(SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");
                tankUpLevel.Background  = selItem.LevelMm > 0 ? Brushes.Green : (Brush)(SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");
                tankUpVolume.Background = selItem.Volume > 0 ? Brushes.Green : (Brush)(SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");
                tankUpMass.Background   = selItem.Mass > 0 ? Brushes.Green : (Brush)(SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");

                tankDownLevelName.Content = "...";
                tankDownLevelValue.Content = "...";

                if (selItem.Level != null)
                {
                    tankDownLevelName.Content  = selItem.Level.TagName;
                    tankDownLevelValue.Content = selItem.Level.Val_R.ToString("f1");
                }

                tankDownDensityTagName.Content = "...";
                tankDownDensityValue.Content = "...";

                if (selItem.Density != null)
                {
                    tankDownDensityTagName.Content = selItem.Density.TagName;
                    tankDownDensityValue.Content = selItem.Density.ValHmi;
                }

                tankDownDistanceATextBox.Text   = selItem.DistanceA.ToString();
                tankDownDistanceBTextBox.Text   = selItem.DistanceB.ToString();
                tankDownProbeLengthTextBox.Text = selItem.ProbeLength.ToString();
                tankDownToDistanceATextBox.Text = selItem.DistToDistanceA.ToString();

                tankDownTypeTextBox.Text = selItem.Tank.type.ToString();
                tankDownDimATextBox.Text = selItem.Tank.dimA.ToString();
                tankDownDimBTextBox.Text = selItem.Tank.dimB.ToString();
                tankDownDimCTextBox.Text = selItem.Tank.dimC.ToString();
                tankDownDimDTextBox.Text = selItem.Tank.dimD.ToString();
                tankDownDimETextBox.Text = selItem.Tank.dimE.ToString();
                tankDownDimFTextBox.Text = selItem.Tank.dimF.ToString();
            }
        }
        #endregion

        #region Tab Item Events
        // Capacities
        private void TabItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ParameterGridCapacity.Visibility = Visibility.Visible;
            ParameterGridDensity.Visibility = Visibility.Hidden;
            ParameterGridContent.Visibility = Visibility.Hidden;
            ParameterGridTank.Visibility = Visibility.Hidden;
            ParameterGridNotSelected.Visibility = Visibility.Hidden;

            string empty = capacityUpTagName.Content.ToString();
            if (empty == "...")
            {
                ParameterGridCapacity.Visibility = Visibility.Hidden;
                ParameterGridNotSelected.Visibility = Visibility.Visible;
            }
        }

        // Densities
        private void TabItem_MouseUp_1(object sender, MouseButtonEventArgs e)
        {
            ParameterGridCapacity.Visibility = Visibility.Hidden;
            ParameterGridDensity.Visibility = Visibility.Visible;
            ParameterGridContent.Visibility = Visibility.Hidden;
            ParameterGridTank.Visibility = Visibility.Hidden;
            ParameterGridNotSelected.Visibility = Visibility.Hidden;

            string empty = densityUpTagName.Content.ToString();
            if (empty == "...")
            {
                ParameterGridDensity.Visibility = Visibility.Hidden;
                ParameterGridNotSelected.Visibility = Visibility.Visible;
            }
        }

        // Contents
        private void TabItem_MouseUp_2(object sender, MouseButtonEventArgs e)
        {
            ParameterGridCapacity.Visibility = Visibility.Hidden;
            ParameterGridDensity.Visibility = Visibility.Hidden;
            ParameterGridContent.Visibility = Visibility.Visible;
            ParameterGridTank.Visibility = Visibility.Hidden;
            ParameterGridNotSelected.Visibility = Visibility.Hidden;

            string empty = contentUpTagName.Content.ToString();
            if (empty == "...")
            {
                ParameterGridContent.Visibility = Visibility.Hidden;
                ParameterGridNotSelected.Visibility = Visibility.Visible;
            }
        }

        // Tanks
        private void TabItem_MouseUp_3(object sender, MouseButtonEventArgs e)
        {
            ParameterGridCapacity.Visibility = Visibility.Hidden;
            ParameterGridDensity.Visibility = Visibility.Hidden;
            ParameterGridContent.Visibility = Visibility.Hidden;
            ParameterGridTank.Visibility = Visibility.Visible;
            ParameterGridNotSelected.Visibility = Visibility.Hidden;

            string empty = tankUpTagName.Content.ToString();
            if (empty == "...")
            {
                ParameterGridTank.Visibility = Visibility.Hidden;
                ParameterGridNotSelected.Visibility = Visibility.Visible;
            }
        }
        #endregion
    }
}
