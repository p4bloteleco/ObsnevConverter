using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text.Json;
using Npgsql;
using System.Security.Cryptography;


namespace ObsnevConverter
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
            button3.Enabled = false;
        }
        public class DbConnectionConfig
        {
            public string Host { get; set; }
            public int Port { get; set; }
            public string Database { get; set; }
            public string Username { get; set; }
            public string EncryptedPassword { get; set; }
        }

        private void button1_Click(object sender, EventArgs e)
        {

            try
            {
                string host = textBox1.Text.Trim();
                string portText = textBox2.Text.Trim();
                string user = textBox3.Text.Trim();
                string passwordPlain = textBox4.Text;   // en claro
                string database = textBox5.Text.Trim();

                if (string.IsNullOrEmpty(host) ||
                    string.IsNullOrEmpty(portText) ||
                    string.IsNullOrEmpty(user) ||
                    string.IsNullOrEmpty(passwordPlain) ||
                    string.IsNullOrEmpty(database))
                {
                    MessageBox.Show("Rellena todos los campos de conexión (1 a 5).",
                        "Campos incompletos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!int.TryParse(portText, out int port))
                {
                    MessageBox.Show("El puerto no es un número válido.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Encriptar la contraseña con DPAPI (solo usuario actual de Windows)
                byte[] clearBytes = Encoding.UTF8.GetBytes(passwordPlain);
                byte[] encryptedBytes = ProtectedData.Protect(
                    clearBytes,
                    null,
                    DataProtectionScope.CurrentUser);

                string encryptedBase64 = Convert.ToBase64String(encryptedBytes);

                var config = new DbConnectionConfig
                {
                    Host = host,
                    Port = port,
                    Database = database,
                    Username = user,
                    EncryptedPassword = encryptedBase64
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(config, jsonOptions);

                richTextBox1.Text = json;
                button3.Enabled = false; // todavía no hemos probado conexión

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error generando JSON:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                string host = textBox1.Text.Trim();
                string portText = textBox2.Text.Trim();
                string user = textBox3.Text.Trim();
                string passwordPlain = textBox4.Text;
                string database = textBox5.Text.Trim();

                if (!int.TryParse(portText, out int port))
                {
                    MessageBox.Show("El puerto no es un número válido.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string connString =
                    $"Host={host};Port={port};Username={user};Password={passwordPlain};Database={database};";

                using (var conn = new NpgsqlConnection(connString))
                {
                    conn.Open();
                }

                MessageBox.Show("Conexión correcta ✅",
                    "Conexión BD", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Si la conexión funciona, permitimos grabar el JSON
                button3.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al probar la conexión:\n" + ex.Message,
                    "Error de conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
                button3.Enabled = false;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                string fileName = textBox6.Text.Trim();
                if (string.IsNullOrEmpty(fileName))
                {
                    MessageBox.Show("Introduce un nombre de fichero (textBox6).",
                        "Nombre de fichero", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(richTextBox1.Text))
                {
                    MessageBox.Show("No hay JSON generado en richTextBox1.",
                        "Sin contenido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string folder = @"C:\cadcon";
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // Asegurarnos de que la extensión sea .json
                if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".json";
                }

                string fullPath = Path.Combine(folder, fileName);

                File.WriteAllText(fullPath, richTextBox1.Text, Encoding.UTF8);

                MessageBox.Show($"JSON guardado en:\n{fullPath}",
                    "Fichero guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar el fichero JSON:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
