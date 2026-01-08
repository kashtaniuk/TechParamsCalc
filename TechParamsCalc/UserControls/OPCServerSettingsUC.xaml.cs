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
using System.Configuration;
using TechParamsCalc.DataBaseConnection;

namespace TechParamsCalc
{
    /// <summary>
    /// Interaction logic for OPCServerSettingsUC.xaml
    /// </summary>
    public partial class OPCServerSettingsUC : UserControl
    {
        public bool isEnableWritingChecked { get; set; }
        public string[] singleTagNamesForRW; //Имена тегов, которые нужно читать отдельно от общих групп тегов
        public OPCServerSettingsUC()
        {
            InitializeComponent();
            // for OPC Settings
            WritingEnableCheckBox.DataContext = this;

            //Восстанавлиеваем настройи из App.config for DB Settings
            this.SQLServerAddressSettingTextBox.Text = Properties.Settings.Default.SQLServerAddress;
            this.SQLServerPortSettingTextBox.Text = Properties.Settings.Default.SQLServerPort;
            this.SQLServerDataBaseNameSettingTextBox.Text = Properties.Settings.Default.SQLDataBase;
            this.SQLServerLoginSettingTextBox.Text = Properties.Settings.Default.SQLServerLogin;
            this.SQLServerPasswordSettingTextBox.Password = Properties.Settings.Default.SQLServerPassword;
            SaveButton.IsEnabled = false;

            //Восстанавлиеваем настройи из App.config for Synhronization Settings
            this.ServerSyncWritingTagTextBox.Text = Properties.Settings.Default.ServerSyncWriteTag;
            this.AtmoPressureTagTextBox.Text = Properties.Settings.Default.AtmoPressureTag;
            this.OtherTagsNamesTextBlock.Text = Properties.Settings.Default.OtherTagsFromOPC.Replace(",", Environment.NewLine);
            SaveButton.IsEnabled = false;

            //Соединяем два массива. В будущем необходимо слелать один набор параметров!!!
            singleTagNamesForRW = new string[] { this.ServerSyncWritingTagTextBox.Text, this.AtmoPressureTagTextBox.Text };
        }

        

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.OPCServerNameSettingTextBox.Text = Properties.Settings.Default.OPCServerName;
            this.OPCServerSubDescSettingTextBox.Text = Properties.Settings.Default.OPCServerSubString;
            SaveButton.IsEnabled = false;
        }

        #region OPC Settings 
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.OPCServerName = this.OPCServerNameSettingTextBox.Text;
            Properties.Settings.Default.OPCServerSubString = this.OPCServerSubDescSettingTextBox.Text;

            Properties.Settings.Default.Save();
            SaveButton.IsEnabled = false;
        }

        private void OPCServerNameSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = true;
        }
       

        private void OPCServerSubDescSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = true;
        }
        #endregion

        #region DB settings
        private void SaveButtonDB_Click(object sender, RoutedEventArgs e)
        {
            //Сохраняем настройки в секции Settings приложения
            Properties.Settings.Default.SQLServerAddress = this.SQLServerAddressSettingTextBox.Text;
            Properties.Settings.Default.SQLServerPort = this.SQLServerPortSettingTextBox.Text;
            Properties.Settings.Default.SQLDataBase = this.SQLServerDataBaseNameSettingTextBox.Text;
            Properties.Settings.Default.SQLServerLogin = this.SQLServerLoginSettingTextBox.Text;
            Properties.Settings.Default.SQLServerPassword = this.SQLServerPasswordSettingTextBox.Password;
            Properties.Settings.Default.Save();

            //Сохраняем строку подключения в App.Config
            try
            {
                string editedConnectionString = DBConnection.GetConnectionString(SQLServerAddressSettingTextBox.Text,
                                                                                 SQLServerPortSettingTextBox.Text,
                                                                                 SQLServerDataBaseNameSettingTextBox.Text,
                                                                                 SQLServerLoginSettingTextBox.Text,
                                                                                 SQLServerPasswordSettingTextBox.Password);

                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var connectionStringsSection = (ConnectionStringsSection)config.GetSection("connectionStrings");
                connectionStringsSection.ConnectionStrings["Default"].ConnectionString = editedConnectionString;

                config.Save();
                ConfigurationManager.RefreshSection("connectionStrings");

            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message);
            }
            MessageBox.Show("For settings applying please restart application!", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);

            SaveButtonDB.IsEnabled = false;
        }

        private void SQLServerAddressSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButtonDB.IsEnabled = true;
        }

        private void SQLServerDataBaseNameSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButtonDB.IsEnabled = true;
        }

        private void SQLServerLoginSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButtonDB.IsEnabled = true;
        }

        private void SQLServerPasswordSettingTextBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            SaveButtonDB.IsEnabled = true;
        }

        private void TestDBButtonDB_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dbContext = new DBPGContext())
                {
                    dbContext.Database.Connection.Open();
                    dbContext.Database.Connection.Close();
                    MessageBox.Show("Connection Ok!", "Check connection", MessageBoxButton.OK, MessageBoxImage.Information);
                }

            }
            catch (Exception)
            {
                MessageBox.Show("Connection Error!", "Check connection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SQLServerPortSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButtonDB.IsEnabled = true;
        }
        #endregion

        #region Synchronization settings
        private void SaveButtonSynchr_Click(object sender, RoutedEventArgs e)
        {
            //Сохраняем настройки в секции Settings приложения
            Properties.Settings.Default.ServerSyncWriteTag = this.ServerSyncWritingTagTextBox.Text;
            Properties.Settings.Default.AtmoPressureTag = this.AtmoPressureTagTextBox.Text;

            Properties.Settings.Default.Save();

            MessageBox.Show("For settings applying please restart application!", "Info", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            SaveButtonSynchr.IsEnabled = false;
        }

        private void AtmoPressureTagTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButtonSynchr.IsEnabled = true;
        }

        private void ServerSyncWritingTagTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButtonSynchr.IsEnabled = true;
        }
        #endregion
    }
}
