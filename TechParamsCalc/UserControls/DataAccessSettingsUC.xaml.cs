using System;
using System.Windows;
using System.Windows.Controls;
using System.Configuration;
using TechParamsCalc.DataBaseConnection;

namespace TechParamsCalc
{
    /// <summary>
    /// Interaction logic for DataAccessSettingsUC.xaml
    /// </summary>
    public partial class DataAccessSettingsUC : UserControl
    {
        public DataAccessSettingsUC()
        {
            InitializeComponent();

            //Восстанавлиеваем настройи из App.config
            this.SQLServerAddressSettingTextBox.Text      = Properties.Settings.Default.SQLServerAddress;
            this.SQLServerPortSettingTextBox.Text         = Properties.Settings.Default.SQLServerPort;
            this.SQLServerDataBaseNameSettingTextBox.Text = Properties.Settings.Default.SQLDataBase;
            this.SQLServerLoginSettingTextBox.Text        = Properties.Settings.Default.SQLServerLogin;
            this.SQLServerPasswordSettingTextBox.Password = Properties.Settings.Default.SQLServerPassword; 
            SaveButton.IsEnabled = false;
        }        

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            //Сохраняем настройки в секции Settings приложения
            Properties.Settings.Default.SQLServerAddress  = this.SQLServerAddressSettingTextBox.Text;
            Properties.Settings.Default.SQLServerPort     = this.SQLServerPortSettingTextBox.Text;
            Properties.Settings.Default.SQLDataBase       = this.SQLServerDataBaseNameSettingTextBox.Text;
            Properties.Settings.Default.SQLServerLogin    = this.SQLServerLoginSettingTextBox.Text;
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
            
            SaveButton.IsEnabled = false;
        }

        private void SQLServerAddressSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = true;
        }

        private void SQLServerDataBaseNameSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = true;
        }

        private void SQLServerLoginSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = true;
        }

        private void SQLServerPasswordSettingTextBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = true;
        }

        private void TestDBButton_Click(object sender, RoutedEventArgs e)
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
            SaveButton.IsEnabled = true;
        }
    }
}
