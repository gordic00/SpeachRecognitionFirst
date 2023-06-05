
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Speech.Recognition;
using System.Windows;
using System.Windows.Controls;

namespace SpeachRecognitionFirst
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static SqlConnection sqlConnection;
        private static SpeechRecognitionEngine recognizer;
        private const string nameOfAssistant = "bob";

        public MainWindow()
        {
            InitializeComponent();
            string connectionString = ConfigurationManager.ConnectionStrings
                ["SpeachRecognitionFirst.Properties.Settings.SpeachRecognitionConnectionString"].ConnectionString;

            // Initialize Connection
            sqlConnection = new SqlConnection(connectionString);
            DisplayCommands();
            DisplayAvailableActions();
        }

        private void LoadSpeechRegognition()
        {
            recognizer = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-RS"));
            var choices = new GrammarBuilder(getChoiceLibrary());
            var grammar = new Grammar(choices);
            recognizer.LoadGrammar(grammar);

            // Add a handler for the speech recognized event.
            recognizer.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(sre_SpeechRecognized);

            // Configure the input to the speech recognizer.
            recognizer.SetInputToDefaultAudioDevice();

            // Start asynchronous, continuous speech recognition.
            recognizer.RecognizeAsync(RecognizeMode.Multiple);
        }

        // Create a simple handler for the SpeechRecognized event.
        private void sre_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            List<KeyValuePair<string, string[]>> commandsAndActions = getCommandsAndActions();

            commandsAndActions.ForEach(c =>
            {
                if (c.Key.Equals(e.Result.Text))
                {
                    try
                    {
                        Process.Start(c.Value[0], c.Value[1]);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("We can not find proces: " + c.Value[0]);
                    }
                }
            });

        }

        // Get Choices for grammar
        private Choices getChoiceLibrary()
        {
            List<KeyValuePair<string, string[]>> commandsAndActions = getCommandsAndActions();
            Choices myChoices = new Choices();
            if (commandsAndActions.Count > 0)
            {
                commandsAndActions.ForEach(c => { myChoices.Add(c.Key); });
            } else
            {
                MessageBox.Show("You don't have actions bind with commands.");
                myChoices.Add("hi");
            }
            return myChoices;
        }

        private void DisplayCommands()
        {
            string query = "SELECT * FROM Commands";
            DataTable model = getDataModel(query);
            CommandList.DisplayMemberPath = "Command";
            CommandList.SelectedValuePath = "Id";
            CommandList.ItemsSource = model.DefaultView;
        }

        private void DisplayAvailableActions()
        {
            string query = "SELECT * FROM Actions";
            DataTable model = getDataModel(query);
            SavedActionsList.DisplayMemberPath = "ActionName";
            SavedActionsList.SelectedValuePath = "Id";
            SavedActionsList.ItemsSource = model.DefaultView;
        }


        private List<KeyValuePair<string, string[]>> getCommandsAndActions()
        {
            List<KeyValuePair<string, string[]>> commAndAction = new List<KeyValuePair<string, string[]>>();
            string query = "SELECT c.Command, a.ActionPath AS 'Action', a.Argument, c.Id FROM CommandActions AS ca INNER JOIN Actions AS a ON ca.ActionId = a.Id INNER JOIN Commands AS c ON ca.CommandId = c.Id";
            DataTable model = getDataModel(query);
            foreach (DataRow row in model.Rows)
            {
                commAndAction.Add(new KeyValuePair<string, string[]>(row.Field<string>("Command"), new string[] { row.Field<string>("Action"), row.Field<string>("Argument"), row.Field<int>("Id").ToString() }));
            }
            return commAndAction;
        }

        private DataTable getDataModel(string query)
        {
            DataTable model = new DataTable();
            try
            {
                SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(query, sqlConnection);

                using (sqlDataAdapter)
                {
                    sqlDataAdapter.Fill(model);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Something went terribly wrong with DB. :/");
            }
            finally
            {
                sqlConnection.Close();
            }
            return model;
        }

        private void ExecuteCommand_Click(object sender, RoutedEventArgs e)
        {
            if (CommandList.SelectedValue != null)
            {
                List<KeyValuePair<string, string[]>> commandsAndActions = getCommandsAndActions();

                commandsAndActions.ForEach(c =>
                {
                    //if (c.Key.Equals(CommandList.SelectedValue))
                    if (c.Value[2].Equals(CommandList.SelectedValue.ToString()))
                    {
                        try
                        {
                            Process.Start(c.Value[0], c.Value[1]);
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("We can not find proces: " + c.Value[0]);
                        }
                    }
                });
            }
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            LoadSpeechRegognition();
            ListenerText.Text = "Listener is active";
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            recognizer.Dispose();
            ListenerText.Text = "Listener is inactive";
        }

        private void CommandList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DisplayActions();
        }

        private void DisplayActions()
        {
            if (CommandList.SelectedValue != null)
            {
                try
                {
                    string query = "SELECT ca.Id, c.Command, a.ActionName FROM CommandActions AS ca INNER JOIN Actions AS a ON ca.ActionId = a.Id INNER JOIN Commands AS c ON ca.CommandId = c.Id WHERE c.Id = @CommandId";
                    SqlCommand sqlCommand = new SqlCommand(query, sqlConnection);
                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(sqlCommand);
                    using (sqlDataAdapter)
                    {
                        sqlCommand.Parameters.AddWithValue("CommandId", CommandList.SelectedValue);
                        DataTable actions = new DataTable();
                        sqlDataAdapter.Fill(actions);
                        ActionList.DisplayMemberPath = "ActionName";
                        ActionList.SelectedValuePath = "Id";
                        ActionList.ItemsSource = actions.DefaultView;
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Something went terribly wrong with DB. :/");
                }
                finally
                {
                    sqlConnection.Close();
                }
            } else
            {
                ActionList.ItemsSource = null;
                ActionList.Items.Clear();
            }
        }

        private void AddCommand_Click(object sender, RoutedEventArgs e)
        {
            if (CommandNameTxt.Text != null && !CommandNameTxt.Text.Equals(""))
            {
                try
                {
                    string query = "INSERT INTO Commands VALUES(@Command)";
                    SqlCommand sqlCommand = new SqlCommand(query, sqlConnection);
                    sqlConnection.Open();
                    sqlCommand.Parameters.AddWithValue("Command", CommandNameTxt.Text);
                    sqlCommand.ExecuteScalar();
                    DisplayCommands();
                    CommandNameTxt.Text = "";
                } catch (Exception)
                {
                    MessageBox.Show("Something went terribly wrong with DB. :/");
                }
                finally
                {
                    sqlConnection.Close();
                }
            }
        }

        private void DeleteCommand_Click(object sender, RoutedEventArgs e)
        {
            if (CommandList.SelectedValue != null)
            {
                try
                {
                    string query = "DELETE FROM Commands WHERE Id = @Id";
                    SqlCommand sqlCommand = new SqlCommand(query, sqlConnection);
                    sqlConnection.Open();
                    sqlCommand.Parameters.AddWithValue("Id", CommandList.SelectedValue);
                    sqlCommand.ExecuteScalar();
                    DisplayCommands();
                    DisplayActions();
                }
                catch (Exception)
                {
                    MessageBox.Show("Something went terribly wrong with DB. :/");
                }
                finally
                {
                    sqlConnection.Close();
                }
            }
        }

        private void AddActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SavedActionsList.SelectedValue != null && CommandList.SelectedValue != null)
            {
                try
                {
                    string query = "INSERT INTO CommandActions VALUES(@CommandId, @ActionId)";
                    SqlCommand sqlCommand = new SqlCommand(query, sqlConnection);
                    sqlConnection.Open();
                    sqlCommand.Parameters.AddWithValue("CommandId", CommandList.SelectedValue);
                    sqlCommand.Parameters.AddWithValue("ActionId", SavedActionsList.SelectedValue);
                    sqlCommand.ExecuteScalar();
                    DisplayActions();
                }
                catch (Exception)
                {
                    MessageBox.Show("Something went terribly wrong with DB. :/");
                }
                finally
                {
                    sqlConnection.Close();
                }
            }
        }

        private void NewActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ActionNameTxt.Text != null && !ActionNameTxt.Text.Equals("") &&
                ActionPathTxt.Text != null && !ActionPathTxt.Text.Equals(""))
            {
                try
                {
                    string query = "INSERT INTO Actions VALUES(@ActionName, @ActionPath, @Argument)";
                    SqlCommand sqlCommand = new SqlCommand(query, sqlConnection);
                    sqlConnection.Open();
                    sqlCommand.Parameters.AddWithValue("ActionName", ActionNameTxt.Text);
                    sqlCommand.Parameters.AddWithValue("ActionPath", ActionPathTxt.Text);
                    sqlCommand.Parameters.AddWithValue("Argument", ActionArgTxt.Text);
                    sqlCommand.ExecuteScalar();
                    DisplayAvailableActions();
                    ActionNameTxt.Text = "";
                    ActionPathTxt.Text = "";
                    ActionArgTxt.Text = "";

                }
                catch (Exception)
                {
                    MessageBox.Show("Something went terribly wrong with DB. :/");
                }
                finally
                {
                    sqlConnection.Close();
                }
            }
        }

        private void DeleteActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SavedActionsList.SelectedValue != null && SavedActionsList.SelectedValue != null)
            {
                try
                {
                    string query = "DELETE FROM Actions WHERE Id = @Id";
                    SqlCommand sqlCommand = new SqlCommand(query, sqlConnection);
                    sqlConnection.Open();
                    sqlCommand.Parameters.AddWithValue("Id", SavedActionsList.SelectedValue);
                    sqlCommand.ExecuteScalar();
                    DisplayCommands();
                    DisplayActions();
                    DisplayAvailableActions();
                }
                catch (Exception)
                {
                    MessageBox.Show("Something went terribly wrong with DB. :/");
                }
                finally
                {
                    sqlConnection.Close();
                }
            }
        }

        private void RemoveActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ActionList.SelectedValue != null)
            {
                try
                {
                    string query = "DELETE FROM CommandActions WHERE Id = @Id";
                    SqlCommand sqlCommand = new SqlCommand(query, sqlConnection);
                    sqlConnection.Open();
                    sqlCommand.Parameters.AddWithValue("Id", ActionList.SelectedValue);
                    sqlCommand.ExecuteScalar();
                    DisplayActions();
                }
                catch (Exception)
                {
                    MessageBox.Show("Something went terribly wrong with DB. :/");
                }
                finally
                {
                    sqlConnection.Close();
                }
            }
        }
    }

}
