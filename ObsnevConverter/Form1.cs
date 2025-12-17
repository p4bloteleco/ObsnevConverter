using System;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Linq;
using Npgsql;
using System.Data;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.DataFormats;

namespace ObsnevConverter
{
    public partial class Form1 : Form
    {

        private const string JsonFolder = @"C:\cadcon";
        private TmsMappingConfig _tmsMapping;
        private const string MappingFilePath = @"C:\cadcon\tms_mapping.json";

        private string _currentConnectionString;  // cadena de conexión activa (del JSON)
        private string _currentCsvFolder;         // carpeta seleccionada con CSV
        private Form2 _form2;
        public class DbConnectionConfig
        {
            public string Host { get; set; }
            public int Port { get; set; }
            public string Database { get; set; }
            public string Username { get; set; }
            public string EncryptedPassword { get; set; }
        }

        public class TmsMappingConfig
        {
            public string Delimiter { get; set; } = ";";
            public bool HasHeader { get; set; } = false;
            public string DateTimeFormat { get; set; } = "yyyy.MM.dd HH:mm";
            public Dictionary<string, int> Columns { get; set; } = new Dictionary<string, int>();
        }

        private class FileProcessResult
        {
            public string FileName { get; set; }
            public int RecordCount { get; set; }
            public DateTime? FirstDate { get; set; }
            public DateTime? LastDate { get; set; }
            public DateTime? FirstNonZeroHumidity { get; set; }
            public string ErrorInfo { get; set; }
            public bool ImportOk { get; set; }
            public int InsertedDb { get; set; }
            public int ErrorsDb { get; set; }
            public int DuplicatesDb { get; set; }
        }



        public Form1()
        {
            InitializeComponent();
            // Cargamos los JSON al iniciar

            LoadJsonFilesIntoCombo();
            ResetUiForNewConfig();
            try
            {
                LoadTmsMapping();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando el mapeo TMS:\n" + ex.Message,
                    "Error mapeo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }


        }

        private bool TryGetSerialFromFileName(string baseName, out long serial)
        {
            serial = 0;

            // 1) Caso antiguo: el nombre entero es un número (ej: "95134001")
            if (long.TryParse(baseName, out serial))
                return true;

            // 2) Nuevo formato: ej. "data_95134001_2025_07_17_0"
            // Buscamos la primera secuencia de al menos 4 dígitos seguidos
            var match = Regex.Match(baseName, @"(\d{4,})");
            if (match.Success && long.TryParse(match.Groups[1].Value, out serial))
                return true;

            // No hemos encontrado un serial válido
            return false;
        }



        private void LoadTmsMapping()
        {
            if (!File.Exists(MappingFilePath))
            {
                throw new FileNotFoundException(
                    $"No se encontró el fichero de mapeo:\n{MappingFilePath}");
            }

            string json = File.ReadAllText(MappingFilePath, Encoding.UTF8);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            _tmsMapping = JsonSerializer.Deserialize<TmsMappingConfig>(json, options);

            if (_tmsMapping == null)
            {
                throw new Exception("No se pudo deserializar el fichero de mapeo tms_mapping.json");
            }

            if (_tmsMapping.Columns == null)
            {
                _tmsMapping.Columns = new Dictionary<string, int>();
            }
        }



        private int? GetIdStationForSerial(NpgsqlConnection conn, long serial)
        {
            using (var cmd = new NpgsqlCommand(@"
        SELECT idstation
        FROM public.cn_stations_tms
        WHERE serial = @serial
        LIMIT 1;", conn))
            {
                cmd.Parameters.AddWithValue("@serial", serial);

                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return null;

                return Convert.ToInt32(result);
            }
        }

        private bool ImportSingleTmsFileToDatabase(
     string baseName,
     out int inserted,
     out int errors,
     out int duplicates,
     out string importErrorMessage,
     IProgress<(string fileLabel, double pct)> progress = null)
        {
            inserted = 0;
            errors = 0;
            duplicates = 0; // de momento no distinguimos duplicados, lo dejamos a 0
            importErrorMessage = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(_currentConnectionString))
                    throw new InvalidOperationException("No hay cadena de conexión activa.");

                if (_tmsMapping == null)
                    throw new InvalidOperationException("El mapeo TMS (_tmsMapping) no está cargado.");

                if (_tmsMapping.Columns == null || _tmsMapping.Columns.Count == 0)
                    throw new InvalidOperationException("El diccionario de columnas (_tmsMapping.Columns) está vacío. Revisa tms_mapping.json.");

                if (string.IsNullOrEmpty(_currentCsvFolder))
                    throw new InvalidOperationException("No hay carpeta CSV seleccionada.");

                string filePath = Path.Combine(_currentCsvFolder, baseName + ".csv");
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("No se encontró el fichero CSV", filePath);

                // 1) Obtener serial desde el nombre del fichero
                if (!TryGetSerialFromFileName(baseName, out long serial))
                    throw new Exception($"No se pudo extraer un número de serie válido del nombre de fichero '{baseName}'.");

                string fileLabel = serial.ToString(); // lo que verá el usuario en label1

                // 2) Comprobar mapeo
                if (!_tmsMapping.Columns.ContainsKey("tsample") ||
                    !_tmsMapping.Columns.ContainsKey("tsoil6") ||
                    !_tmsMapping.Columns.ContainsKey("tair2") ||
                    !_tmsMapping.Columns.ContainsKey("tair15") ||
                    !_tmsMapping.Columns.ContainsKey("vwc6"))
                {
                    throw new Exception(
                        "El mapeo de columnas no contiene alguna de las claves esperadas: " +
                        "tsample, tsoil6, tair2, tair15, vwc6. Revisa el contenido de tms_mapping.json.");
                }

                // 3) Contar líneas para el porcentaje
                int totalLines = File.ReadLines(filePath).Count();
                if (totalLines <= 0)
                {
                    progress?.Report((fileLabel, 100.0));
                    return true;
                }

                using (var conn = new NpgsqlConnection(_currentConnectionString))
                {
                    conn.Open();

                    // 4) Buscar idstation en cn_stations_tms
                    int? idStation = GetIdStationForSerial(conn, serial);
                    if (idStation == null)
                        throw new Exception($"No se encontró idstation en cn_stations_tms para serial={serial}.");

                    // 5) Preparar comando de inserción
                    using (var cmd = new NpgsqlCommand(@"
                        INSERT INTO public.tms_data
                            (idstation, serial, tsample, tsoil6, tair2, tair15, vwc6, validation_id)
                        VALUES
                            (@idstation, @serial, @tsample, @tsoil6, @tair2, @tair15, @vwc6, NULL)
                        ON CONFLICT (serial, tsample) DO NOTHING;", conn))
                    {
                        cmd.Parameters.Add("@idstation", NpgsqlTypes.NpgsqlDbType.Integer);
                        cmd.Parameters.Add("@serial", NpgsqlTypes.NpgsqlDbType.Bigint);
                        cmd.Parameters.Add("@tsample", NpgsqlTypes.NpgsqlDbType.TimestampTz);
                        cmd.Parameters.Add("@tsoil6", NpgsqlTypes.NpgsqlDbType.Double);
                        cmd.Parameters.Add("@tair2", NpgsqlTypes.NpgsqlDbType.Double);
                        cmd.Parameters.Add("@tair15", NpgsqlTypes.NpgsqlDbType.Double);
                        cmd.Parameters.Add("@vwc6", NpgsqlTypes.NpgsqlDbType.Double);

                        cmd.Prepare();

                        char delimiter = ';';
                        if (!string.IsNullOrEmpty(_tmsMapping.Delimiter))
                            delimiter = _tmsMapping.Delimiter[0];

                        int idxTs = _tmsMapping.Columns["tsample"];
                        int idxTsoil6 = _tmsMapping.Columns["tsoil6"];
                        int idxTair2 = _tmsMapping.Columns["tair2"];
                        int idxTair15 = _tmsMapping.Columns["tair15"];
                        int idxVwc6 = _tmsMapping.Columns["vwc6"];

                        string dtFormat = _tmsMapping.DateTimeFormat;

                        int lineNumber = 0;
                        int processedLines = 0;

                        // Inicializamos el progreso a 0%
                        progress?.Report((fileLabel, 0.0));

                        foreach (var line in File.ReadLines(filePath))
                        {
                            lineNumber++;
                            string trimmed = line.Trim();
                            if (string.IsNullOrEmpty(trimmed))
                                continue;

                            // Saltar cabecera si la hay
                            if (lineNumber == 1 && _tmsMapping.HasHeader)
                                continue;

                            processedLines++;
                            string[] parts = trimmed.Split(delimiter);

                            try
                            {
                                int minNeeded = Math.Max(idxVwc6,
                                                    Math.Max(idxTs,
                                                        Math.Max(idxTsoil6, Math.Max(idxTair2, idxTair15))));
                                if (parts.Length <= minNeeded)
                                {
                                    errors++;
                                }
                                else
                                {
                                    // Parseo de fecha sin AssumeLocal
                                    if (!DateTime.TryParseExact(
                                            parts[idxTs].Trim(),
                                            dtFormat,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.None,
                                            out DateTime tsample))
                                    {
                                        errors++;
                                    }
                                    else
                                    {
                                        // Marcar como UTC para timestamptz
                                        tsample = DateTime.SpecifyKind(tsample, DateTimeKind.Utc);

                                        double tsoil6Val, tair2Val, tair15Val, vwc6Val;
                                        bool okTsoil6 = double.TryParse(parts[idxTsoil6].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tsoil6Val);
                                        bool okTair2 = double.TryParse(parts[idxTair2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tair2Val);
                                        bool okTair15 = double.TryParse(parts[idxTair15].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tair15Val);
                                        bool okVwc6 = double.TryParse(parts[idxVwc6].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out vwc6Val);

                                        cmd.Parameters["@idstation"].Value = idStation.Value;
                                        cmd.Parameters["@serial"].Value = serial;
                                        cmd.Parameters["@tsample"].Value = tsample;

                                        cmd.Parameters["@tsoil6"].Value = okTsoil6 ? (object)tsoil6Val : DBNull.Value;
                                        cmd.Parameters["@tair2"].Value = okTair2 ? (object)tair2Val : DBNull.Value;
                                        cmd.Parameters["@tair15"].Value = okTair15 ? (object)tair15Val : DBNull.Value;
                                        cmd.Parameters["@vwc6"].Value = okVwc6 ? (object)vwc6Val : DBNull.Value;

                                        try
                                        {
                                            int affected = cmd.ExecuteNonQuery();

                                            if (affected == 1)
                                            {
                                                // Se ha insertado una fila nueva
                                                inserted++;
                                            }
                                            else
                                            {
                                                // ON CONFLICT DO NOTHING -> 0 filas afectadas = duplicado
                                                duplicates++;
                                            }
                                        }
                                        catch
                                        {
                                            // Cualquier otro error de BD (tipos, FK, etc.)
                                            errors++;
                                        }

                                    }
                                }
                            }
                            catch
                            {
                                errors++;
                            }

                            // Actualizar porcentaje después de procesar la línea
                            double pct = processedLines * 100.0 / totalLines;
                            progress?.Report((fileLabel, pct));
                        }

                        // Aseguramos llegar a 100%
                        progress?.Report((fileLabel, 100.0));
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                importErrorMessage = ex.Message;
                return false;
            }
        }



        private void LoadJsonFilesIntoCombo()
        {
            comboBox1.Items.Clear();

            if (!Directory.Exists(JsonFolder))
            {
                // Si no existe, no pasa nada, solo no habrá opciones
                return;
            }

            var files = Directory.GetFiles(JsonFolder, "*.json");
            foreach (var file in files)
            {
                // Mostramos solo el nombre de fichero, pero podríamos guardar ruta completa si quisieras
                comboBox1.Items.Add(Path.GetFileName(file));
            }

            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
                button5.Enabled = true;
            }
            else
            {
                button5.Enabled = false;
            }
        }
        private string DecryptPassword(string encryptedBase64)
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
            byte[] clearBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(clearBytes);
        }

        private void ResetUiForNewConfig()
        {
            // Sin conexión activa ni carpeta CSV
            _currentConnectionString = null;
            _currentCsvFolder = null;

            // Indicador de conexión
            checkBox1.Checked = false;
            checkBox1.Enabled = false;

            // Limpiar labels de estado
            label1.Text = string.Empty;  // nombre fichero en curso
            label2.Text = string.Empty;  // porcentaje

            // Limpiar listas y grid
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            dataGridView2.DataSource = null;
            dataGridView2.Rows.Clear();
            dataGridView2.Columns.Clear();

            // Deshabilitar botones que dependen de conexión o datos
            button1.Enabled = false;   // seleccionar carpeta CSV
            button2.Enabled = false;   // mover de listBox2 -> listBox1
            button3.Enabled = false;   // procesar seleccionados
            button4.Enabled = false;   // mover de listBox1 -> listBox2
                                       // button5: test conexión -> lo controlamos aparte
                                       // button6: abrir Form2 (config) -> siempre habilitado si quieres


            // Solo se permite probar conexión (si hay JSON)
            button5.Enabled = comboBox1.Items.Count > 0;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem == null)
            {
                MessageBox.Show("Selecciona primero un fichero JSON en el combo.",
                    "Sin selección", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string fileName = comboBox1.SelectedItem.ToString();
            string fullPath = Path.Combine(JsonFolder, fileName);

            if (!File.Exists(fullPath))
            {
                MessageBox.Show("El fichero JSON seleccionado no existe:\n" + fullPath,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string json = File.ReadAllText(fullPath, Encoding.UTF8);
                var config = JsonSerializer.Deserialize<DbConnectionConfig>(json);

                if (config == null)
                {
                    MessageBox.Show("No se pudo deserializar el JSON.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string password = DecryptPassword(config.EncryptedPassword);

                string connString =
                    $"Host={config.Host};Port={config.Port};Username={config.Username};" +
                    $"Password={password};Database={config.Database};";

                // Probar conexión
                using (var conn = new NpgsqlConnection(connString))
                {
                    conn.Open();
                }

                // Conexión OK
                _currentConnectionString = connString;

                // Indicador de conexión
                checkBox1.Enabled = true;
                checkBox1.Checked = true;

                // Habilitar controles que dependen de tener conexión
                button1.Enabled = true;        // seleccionar carpeta CSV
                listBox1.Enabled = true;
                listBox2.Enabled = true;
                dataGridView2.Enabled = true;

                // Botones de movimiento/procesado se habilitarán cuando haya datos
                button2.Enabled = true;       // listBox2 -> listBox1
                button3.Enabled = true;       // procesar seleccionados
                button4.Enabled = true;       // listBox1 -> listBox2

                MessageBox.Show("Conexión correcta",
                    "BD", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _currentConnectionString = null;

                checkBox1.Checked = false;
                checkBox1.Enabled = false;

                button1.Enabled = false;
                listBox1.Enabled = false;
                listBox2.Enabled = false;
                dataGridView2.Enabled = false;

                button2.Enabled = false;
                button3.Enabled = false;
                button4.Enabled = false;

                MessageBox.Show("Error al probar la conexión:\n" + ex.Message,
                    "Error de conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            // Si no existe aún o se ha cerrado (Disposed), creamos uno nuevo
            if (_form2 == null || _form2.IsDisposed)
            {
                _form2 = new Form2();
                _form2.Owner = this;   // opcional, para que Form1 sea el “dueño”
                _form2.Show();
            }
            else
            {
                // Ya está abierto: lo traemos al frente
                if (_form2.WindowState == FormWindowState.Minimized)
                {
                    _form2.WindowState = FormWindowState.Normal;
                }

                _form2.BringToFront();
                _form2.Focus();
            }
        }

        private void MoveSelectedItems(ListBox from, ListBox to)
        {
            // Copiamos primero a una lista para no modificar la colección mientras iteramos
            var itemsToMove = new List<object>();
            foreach (var item in from.SelectedItems)
            {
                itemsToMove.Add(item);
            }

            // Movemos: añadimos al destino y quitamos del origen
            foreach (var item in itemsToMove)
            {
                if (!to.Items.Contains(item))
                {
                    to.Items.Add(item);
                }
                from.Items.Remove(item);
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            ResetUiForNewConfig();
        }


        private void LoadCsvFilesIntoListBox(string folder)
        {
            listBox1.Items.Clear();

            if (!Directory.Exists(folder))
            {
                MessageBox.Show("La carpeta seleccionada no existe:\n" + folder,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var files = Directory.GetFiles(folder, "*.csv");

            foreach (var file in files)
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                listBox1.Items.Add(fileNameWithoutExt);
            }

            if (files.Length == 0)
            {
                MessageBox.Show("No se han encontrado ficheros .csv en la carpeta seleccionada.",
                    "Sin ficheros", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecciona uno o varios ficheros en la lista de la izquierda (listBox1).",
                    "Sin selección", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MoveSelectedItems(listBox1, listBox2);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentConnectionString))
            {
                MessageBox.Show("No hay conexión activa. Prueba primero la conexión con el botón 5.",
                    "Sin conexión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Selecciona la carpeta con los ficheros CSV de TMS";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _currentCsvFolder = dlg.SelectedPath;
                    LoadCsvFilesIntoListBox(_currentCsvFolder);
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {

            if (listBox2.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecciona uno o varios ficheros en la lista de la derecha (listBox2).",
                    "Sin selección", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MoveSelectedItems(listBox2, listBox1);

        }

        private bool TryParseLineForStats(string line, out DateTime tsample, out double vwc6)
        {
            tsample = default;
            vwc6 = double.NaN;

            if (string.IsNullOrWhiteSpace(line))
                return false;

            var parts = line.Split(';');
            if (parts.Length < 8)
                return false;

            // parts[1] = "2025.04.02 19:45"
            if (!DateTime.TryParseExact(
                    parts[1].Trim(),
                    "yyyy.MM.dd HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out tsample))
            {
                return false;
            }

            // parts[7] = vwc6
            if (!double.TryParse(
                    parts[7].Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out vwc6))
            {
                return false;
            }

            return true;
        }

        private void ComputeStatsForFile(
    string filePath,
    out int recordCount,
    out DateTime? firstDate,
    out DateTime? lastDate,
    out DateTime? firstNonZeroHumidity,
    out int errorCount,
    out int? firstErrorLine,
    Action<int>? reportProgress = null)
        {
            recordCount = 0;
            firstDate = null;
            lastDate = null;
            firstNonZeroHumidity = null;
            errorCount = 0;
            firstErrorLine = null;

            if (!File.Exists(filePath))
                return;

            // Contamos las líneas para poder calcular porcentaje
            int totalLines = File.ReadLines(filePath).Count();
            if (totalLines == 0)
            {
                reportProgress?.Invoke(100);
                return;
            }

            int lineNumber = 0;

            foreach (var line in File.ReadLines(filePath))
            {
                lineNumber++;

                if (!TryParseLineForStats(line, out DateTime tsample, out double vwc6))
                {
                    errorCount++;
                    if (firstErrorLine == null)
                        firstErrorLine = lineNumber;
                }
                else
                {
                    recordCount++;

                    if (firstDate == null || tsample < firstDate.Value)
                        firstDate = tsample;

                    if (lastDate == null || tsample > lastDate.Value)
                        lastDate = tsample;

                    if (firstNonZeroHumidity == null && Math.Abs(vwc6) > double.Epsilon)
                        firstNonZeroHumidity = tsample;
                }

                if (reportProgress != null)
                {
                    // Porcentaje aproximado, para no actualizar cada línea usamos un throttle simple
                    if (lineNumber % 500 == 0 || lineNumber == totalLines)
                    {
                        int pct = (int)(lineNumber * 100L / totalLines);
                        reportProgress(pct);
                    }
                }
            }

            // por si no ha llegado justo a 100
            reportProgress?.Invoke(100);
        }


        private async void button3_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentCsvFolder))
            {
                MessageBox.Show("Primero selecciona una carpeta con CSV usando el botón 1.",
                    "Carpeta no seleccionada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (listBox2.Items.Count == 0)
            {
                MessageBox.Show("No hay ficheros seleccionados en la lista de la derecha (listBox2).",
                    "Sin ficheros", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Deshabilitamos botones mientras se procesa
            button1.Enabled = false;
            button2.Enabled = false;
            button3.Enabled = false;
            button4.Enabled = false;

            // Copiamos la lista de nombres para no depender de la UI dentro del Task
            var filesToProcess = listBox2.Items.Cast<object>()
                                               .Select(o => o.ToString())
                                               .ToList();

            // Preparamos el DataTable y lo enlazamos al grid ANTES del procesamiento
            var table = new DataTable();
            table.Columns.Add("Fichero", typeof(string));
            table.Columns.Add("Registros", typeof(int));
            table.Columns.Add("PrimeraFecha", typeof(DateTime));
            table.Columns.Add("UltimaFecha", typeof(DateTime));
            table.Columns.Add("PrimeraHumNoCero", typeof(DateTime));
            table.Columns.Add("Errores", typeof(string));
            table.Columns.Add("Estado", typeof(string));   // OK / ERROR

            dataGridView2.AutoGenerateColumns = true;
            dataGridView2.DataSource = table;

            // Progreso de líneas: actualiza label1 (serial) y label2 (% con 2 decimales)
            var progressLines = new Progress<(string fileLabel, double pct)>(info =>
            {
                label1.Text = info.fileLabel;                     // serial de la estación
                label2.Text = info.pct.ToString("0.00") + " %";   // porcentaje de líneas del fichero actual
            });

            // Progreso de ficheros: cuando acaba un fichero, añadimos la fila al grid
            var progressFiles = new Progress<FileProcessResult>(res =>
            {
                var row = table.NewRow();
                row["Fichero"] = res.FileName;
                row["Registros"] = res.RecordCount;
                row["PrimeraFecha"] = (object?)res.FirstDate ?? DBNull.Value;
                row["UltimaFecha"] = (object?)res.LastDate ?? DBNull.Value;
                row["PrimeraHumNoCero"] = (object?)res.FirstNonZeroHumidity ?? DBNull.Value;
                row["Errores"] = res.ErrorInfo;
                row["Estado"] = res.ImportOk ? "OK" : "ERROR";
                table.Rows.Add(row);

                // Colorear la fila recién añadida
                var dgvRow = dataGridView2.Rows[dataGridView2.Rows.Count - 1];
                if (res.ImportOk)
                    dgvRow.DefaultCellStyle.BackColor = Color.LightGreen;
                else
                    dgvRow.DefaultCellStyle.BackColor = Color.LightCoral;
            });

            // Ejecutamos el procesado pesado en segundo plano
            var summary = await Task.Run(() =>
            {
                int totalFiles = 0;
                int totalInserted = 0;
                int totalErrors = 0;
                int totalDuplicates = 0;

                foreach (var baseName in filesToProcess)
                {
                    string filePath = Path.Combine(_currentCsvFolder, baseName + ".csv");

                    int recordCount;
                    DateTime? firstDate;
                    DateTime? lastDate;
                    DateTime? firstNonZeroHumidity;
                    int errorCount;
                    int? firstErrorLine;

                    // Estadísticas (sin tocar la UI)
                    ComputeStatsForFile(
                        filePath,
                        out recordCount,
                        out firstDate,
                        out lastDate,
                        out firstNonZeroHumidity,
                        out errorCount,
                        out firstErrorLine,
                        null);

                    string errorInfo;
                    if (!File.Exists(filePath))
                    {
                        errorInfo = "Fichero no encontrado";
                    }
                    else if (recordCount == 0 && errorCount > 0)
                    {
                        errorInfo = $"Sin registros válidos. Errores de formato en {errorCount} línea(s). " +
                                    $"Primera línea con error: {firstErrorLine}";
                    }
                    else if (errorCount > 0)
                    {
                        errorInfo = $"OK con errores: {errorCount} línea(s) con formato incorrecto. " +
                                    $"Primera línea con error: {firstErrorLine}";
                    }
                    else
                    {
                        errorInfo = "OK";
                    }

                    // Importación a BD con progreso de líneas
                    bool importOk;
                    int insertedDb;
                    int errorsDb;
                    int duplicatesDb;
                    string importErrorMessage;

                    importOk = ImportSingleTmsFileToDatabase(
                        baseName,
                        out insertedDb,
                        out errorsDb,
                        out duplicatesDb,
                        out importErrorMessage,
                        progressLines);

                    totalFiles++;
                    totalInserted += insertedDb;
                    totalErrors += errorsDb;
                    totalDuplicates += duplicatesDb;

                    if (!importOk && !string.IsNullOrEmpty(importErrorMessage))
                    {
                        errorInfo += " | Error BD: " + importErrorMessage;
                    }

                    errorInfo += $" | Insertadas: {insertedDb}, Errores BD: {errorsDb}, Duplicadas: {duplicatesDb}";

                    var fileResult = new FileProcessResult
                    {
                        FileName = baseName,
                        RecordCount = recordCount,
                        FirstDate = firstDate,
                        LastDate = lastDate,
                        FirstNonZeroHumidity = firstNonZeroHumidity,
                        ErrorInfo = errorInfo,
                        ImportOk = importOk,
                        InsertedDb = insertedDb,
                        ErrorsDb = errorsDb,
                        DuplicatesDb = duplicatesDb
                    };

                    // Avisamos al progreso de ficheros: se añade una fila al grid en el hilo de UI
                    ((IProgress<FileProcessResult>)progressFiles).Report(fileResult);
                }

                return (totalFiles, totalInserted, totalErrors, totalDuplicates);
            });

            // De vuelta al hilo de UI

            // Limpiamos labels
            label1.Text = string.Empty;
            label2.Text = string.Empty;

            // Quitamos ficheros procesados de listBox2
            foreach (var name in filesToProcess)
            {
                listBox2.Items.Remove(name);
            }

            // Rehabilitamos botones
            button1.Enabled = true;
            button4.Enabled = listBox1.Items.Count > 0;
            button2.Enabled = listBox2.Items.Count > 0;
            button3.Enabled = listBox2.Items.Count > 0;

            // Resumen final
            MessageBox.Show(
                $"Ficheros procesados: {summary.totalFiles}\n" +
                $"Filas insertadas: {summary.totalInserted}\n" +
                $"Filas con error (parseo/BD): {summary.totalErrors}\n" +
                $"Filas duplicadas (ignoradas): {summary.totalDuplicates}",
                "Resumen importación TMS",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }


        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentCsvFolder))
            {
                MessageBox.Show("Primero selecciona una carpeta con CSV (botón 1).",
                    "Carpeta no seleccionada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (listBox1.SelectedItem == null)
            {
                MessageBox.Show("No hay ningún fichero seleccionado en la lista.",
                    "Sin selección", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string baseName = listBox1.SelectedItem.ToString();
            string filePath = Path.Combine(_currentCsvFolder, baseName + ".csv");

            if (!File.Exists(filePath))
            {
                MessageBox.Show("El fichero no existe:\n" + filePath,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = "\"" + filePath + "\"",
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir el fichero en el Bloc de notas:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            var sb = new StringBuilder();

            try
            {
                if (string.IsNullOrEmpty(_currentConnectionString))
                    throw new InvalidOperationException("No hay cadena de conexión activa.");

                if (_tmsMapping == null)
                    throw new InvalidOperationException("El mapeo TMS (_tmsMapping) no está cargado.");

                if (_tmsMapping.Columns == null || _tmsMapping.Columns.Count == 0)
                    throw new InvalidOperationException("El diccionario de columnas (_tmsMapping.Columns) está vacío. Revisa tms_mapping.json.");

                if (string.IsNullOrEmpty(_currentCsvFolder))
                    throw new InvalidOperationException("No hay carpeta CSV seleccionada.");

                if (listBox2.Items.Count == 0)
                    throw new InvalidOperationException("No hay ningún fichero en listBox2. Mueve alguno con el botón 4.");

                // 1) Fichero: el primero de listBox2
                string baseName = listBox2.Items[0].ToString();
                string filePath = Path.Combine(_currentCsvFolder, baseName + ".csv");

                sb.AppendLine($"Fichero baseName: {baseName}");
                sb.AppendLine($"Ruta completa: {filePath}");

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("No se encontró el fichero CSV", filePath);

                // 2) Serial a partir del nombre del fichero
                if (!TryGetSerialFromFileName(baseName, out long serial))
                    throw new Exception($"No se pudo extraer un número de serie válido del nombre de fichero '{baseName}'.");
                sb.AppendLine($"Serial (desde nombre fichero): {serial}");

                // 3) Mostrar mapeo
                sb.AppendLine("Contenido de _tmsMapping.Columns:");
                foreach (var kv in _tmsMapping.Columns)
                {
                    sb.AppendLine($"  {kv.Key} -> {kv.Value}");
                }

                // 4) Tomar la primera línea válida del CSV
                char delimiter = ';';
                if (!string.IsNullOrEmpty(_tmsMapping.Delimiter))
                    delimiter = _tmsMapping.Delimiter[0];

                string dtFormat = _tmsMapping.DateTimeFormat;

                if (!_tmsMapping.Columns.ContainsKey("tsample") ||
                    !_tmsMapping.Columns.ContainsKey("tsoil6") ||
                    !_tmsMapping.Columns.ContainsKey("tair2") ||
                    !_tmsMapping.Columns.ContainsKey("tair15") ||
                    !_tmsMapping.Columns.ContainsKey("vwc6"))
                {
                    throw new Exception("Faltan claves en el mapeo (tsample, tsoil6, tair2, tair15, vwc6).");
                }

                int idxTs = _tmsMapping.Columns["tsample"];
                int idxTsoil6 = _tmsMapping.Columns["tsoil6"];
                int idxTair2 = _tmsMapping.Columns["tair2"];
                int idxTair15 = _tmsMapping.Columns["tair15"];
                int idxVwc6 = _tmsMapping.Columns["vwc6"];

                string rawLine = null;
                string[] parts = null;

                int lineNumber = 0;
                foreach (var line in File.ReadLines(filePath))
                {
                    lineNumber++;
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed))
                        continue;

                    if (lineNumber == 1 && _tmsMapping.HasHeader)
                        continue;

                    rawLine = trimmed;
                    parts = trimmed.Split(delimiter);
                    break; // solo la primera línea válida
                }

                if (rawLine == null || parts == null)
                    throw new Exception("No se encontró ninguna línea válida en el fichero.");

                sb.AppendLine();
                sb.AppendLine($"Línea #{lineNumber} bruta:");
                sb.AppendLine(rawLine);

                // 5) Mostrar campos individuales usados
                sb.AppendLine();
                sb.AppendLine("Campos extraídos según índices:");
                sb.AppendLine($"  parts[{idxTs}]     (tsample) = {SafeGet(parts, idxTs)}");
                sb.AppendLine($"  parts[{idxTsoil6}] (tsoil6)  = {SafeGet(parts, idxTsoil6)}");
                sb.AppendLine($"  parts[{idxTair2}]  (tair2)   = {SafeGet(parts, idxTair2)}");
                sb.AppendLine($"  parts[{idxTair15}] (tair15)  = {SafeGet(parts, idxTair15)}");
                sb.AppendLine($"  parts[{idxVwc6}]   (vwc6)    = {SafeGet(parts, idxVwc6)}");

                // Parseo
                if (!DateTime.TryParseExact(
                        parts[idxTs].Trim(),
                        dtFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,   // sin AssumeLocal
                        out DateTime tsample))
                {
                    throw new Exception($"No se pudo parsear la fecha/hora '{parts[idxTs]}' con formato '{dtFormat}'.");
                }

                // Igual que en la importación real: marcamos como UTC para timestamptz
                tsample = DateTime.SpecifyKind(tsample, DateTimeKind.Utc);


                double tsoil6Val, tair2Val, tair15Val, vwc6Val;
                bool okTsoil6 = double.TryParse(parts[idxTsoil6].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tsoil6Val);
                bool okTair2 = double.TryParse(parts[idxTair2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tair2Val);
                bool okTair15 = double.TryParse(parts[idxTair15].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tair15Val);
                bool okVwc6 = double.TryParse(parts[idxVwc6].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out vwc6Val);

                sb.AppendLine();
                sb.AppendLine("Valores parseados:");
                sb.AppendLine($"  tsample = {tsample:o}"); // formato ISO
                sb.AppendLine($"  tsoil6  = {(okTsoil6 ? tsoil6Val.ToString(CultureInfo.InvariantCulture) : "ERROR")}");
                sb.AppendLine($"  tair2   = {(okTair2 ? tair2Val.ToString(CultureInfo.InvariantCulture) : "ERROR")}");
                sb.AppendLine($"  tair15  = {(okTair15 ? tair15Val.ToString(CultureInfo.InvariantCulture) : "ERROR")}");
                sb.AppendLine($"  vwc6    = {(okVwc6 ? vwc6Val.ToString(CultureInfo.InvariantCulture) : "ERROR")}");

                // 6) Intentar insertar UNA fila en la BD
                using (var conn = new NpgsqlConnection(_currentConnectionString))
                {
                    conn.Open();

                    int? idStation = GetIdStationForSerial(conn, serial);
                    if (idStation == null)
                        throw new Exception($"No se encontró idstation en cn_stations_tms para serial={serial}.");

                    sb.AppendLine();
                    sb.AppendLine($"idstation encontrado en BD: {idStation.Value}");

                    using (var cmd = new NpgsqlCommand(@"
                        INSERT INTO public.tms_data
                            (idstation, serial, tsample, tsoil6, tair2, tair15, vwc6, validation_id)
                        VALUES
                            (@idstation, @serial, @tsample, @tsoil6, @tair2, @tair15, @vwc6, NULL)
                        ON CONFLICT (serial, tsample) DO NOTHING;", conn))

                    {
                        cmd.Parameters.AddWithValue("@idstation", idStation.Value);
                        cmd.Parameters.AddWithValue("@serial", serial);
                        cmd.Parameters.AddWithValue("@tsample", tsample);

                        if (okTsoil6) cmd.Parameters.AddWithValue("@tsoil6", tsoil6Val);
                        else cmd.Parameters.AddWithValue("@tsoil6", DBNull.Value);

                        if (okTair2) cmd.Parameters.AddWithValue("@tair2", tair2Val);
                        else cmd.Parameters.AddWithValue("@tair2", DBNull.Value);

                        if (okTair15) cmd.Parameters.AddWithValue("@tair15", tair15Val);
                        else cmd.Parameters.AddWithValue("@tair15", DBNull.Value);

                        if (okVwc6) cmd.Parameters.AddWithValue("@vwc6", vwc6Val);
                        else cmd.Parameters.AddWithValue("@vwc6", DBNull.Value);

                        sb.AppendLine();
                        sb.AppendLine("Comando SQL:");
                        sb.AppendLine(cmd.CommandText);

                        sb.AppendLine();
                        sb.AppendLine("Parámetros:");
                        foreach (NpgsqlParameter p in cmd.Parameters)
                        {
                            sb.AppendLine($"  {p.ParameterName} = {p.Value} (tipo {p.NpgsqlDbType})");
                        }

                        int affected = cmd.ExecuteNonQuery();
                        sb.AppendLine();
                        sb.AppendLine($"Resultado ExecuteNonQuery(): {affected} fila(s) afectada(s).");
                        if (affected == 0)
                            sb.AppendLine("Nota: La fila ya existía (duplicado por serial+tsample).");
                    }
                }

                MessageBox.Show(sb.ToString(), "Test inserción 1 fila OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine("EXCEPCIÓN:");
                sb.AppendLine(ex.ToString());

                MessageBox.Show(sb.ToString(), "Test inserción 1 fila - ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Helper para no petar si el índice se sale
        private string SafeGet(string[] parts, int index)
        {
            if (index < 0 || index >= parts.Length) return "<OUT OF RANGE>";
            return parts[index];
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }
    }
}
