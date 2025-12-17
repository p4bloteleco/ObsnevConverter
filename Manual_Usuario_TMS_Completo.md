
# Manual de Usuario – Aplicación de Integración de Datos TMS

## 1. Introducción

Esta aplicación ha sido desarrollada para facilitar la **integración controlada de datos ambientales**
procedentes de sensores TMS (Time-series Monitoring Sensors) en una base de datos científica basada en
PostgreSQL, PostGIS y TimescaleDB.

Está pensada para **usuarios no técnicos**, permitiendo realizar todo el proceso sin necesidad de
programar ni conocer SQL.

---

## 2. Requisitos previos

Para que la aplicación funcione correctamente es necesario:

### 2.1 Infraestructura

- Un servidor PostgreSQL operativo.
- Extensiones instaladas:
  - PostGIS
  - TimescaleDB
- Acceso a la base de datos (host, puerto, usuario y contraseña).

### 2.2 Estructura mínima de la base de datos

La base de datos debe disponer al menos de las siguientes tablas:

- **cn_stations**  
  Contiene las estaciones principales (ubicación, red, estado).

- **cn_stations_tms**  
  Contiene los sensores TMS asociados a cada estación.

- **tms_data**  
  Tabla de series temporales donde se almacenan las medidas de los sensores.
  Esta tabla debe estar configurada como *hypertable* en TimescaleDB.

Estas tablas deben estar relacionadas entre sí mediante identificadores internos.

---

## 3. Estructura de la aplicación

La aplicación consta de dos pantallas principales:

- **Formulario de Configuración (Form2)**  
- **Formulario Principal de Procesado (Form1)**

El uso correcto exige **configurar primero Form2** y después trabajar con Form1.

---

## 4. Formulario de Configuración (Form2)

### 4.1 Objetivo de Form2

Form2 permite crear y gestionar **ficheros de conexión** a la base de datos.
Sin una conexión válida creada en Form2, **Form1 no puede funcionar**.

### 4.2 Campos del formulario

El usuario debe introducir:

- Dirección del servidor (por ejemplo: servidor.midominio.es)
- Puerto (normalmente 5432)
- Nombre de la base de datos
- Usuario de base de datos
- Contraseña
- Nombre del fichero de configuración

### 4.3 Generar cadena de conexión

Al pulsar el botón **Generar**:
- Se crea la cadena de conexión.
- La contraseña se cifra automáticamente.
- La cadena aparece en el área de texto.

### 4.4 Probar conexión

Al pulsar **Probar conexión**:
- La aplicación intenta conectarse al servidor.
- Si la conexión es correcta, se habilita el guardado.

### 4.5 Guardar configuración

Al pulsar **Guardar**:
- Se crea un fichero `.json` en `C:\cadcon\`.
- Este fichero será usado posteriormente por Form1.

---

## 5. Formulario Principal (Form1)

### 5.1 Objetivo de Form1

Form1 permite:
- Seleccionar ficheros CSV de sensores.
- Analizarlos.
- Insertar sus datos en la base de datos.

### 5.2 Selección de configuración

- Seleccione un fichero JSON en el desplegable.
- Pulse **Probar conexión**.
- Si la conexión es válida, se activan los controles.

---

## 6. Selección de ficheros CSV

### 6.1 Seleccionar carpeta

Pulse **Seleccionar carpeta** y elija la carpeta donde se encuentran los CSV.
Los ficheros aparecerán en la lista izquierda.

### 6.2 Visualización rápida

Doble clic sobre un fichero para abrirlo en el Bloc de notas.

### 6.3 Selección de ficheros a procesar

- Seleccione uno o varios ficheros.
- Use las flechas para moverlos a la lista de procesado.

---

## 7. Procesado de datos

Al pulsar **Procesar**:

- Cada fichero se analiza línea a línea.
- Se calculan estadísticas:
  - Número de registros.
  - Fechas inicial y final.
  - Primera medición válida.
- Se detectan errores de formato.

El progreso se muestra en pantalla.

---

## 8. Inserción en base de datos

- Los datos se insertan en la tabla `tms_data`.
- Los registros duplicados se ignoran automáticamente.
- La aplicación garantiza que no se pierdan datos existentes.

---

## 9. Seguridad y fiabilidad

- No se eliminan datos.
- No se duplican registros.
- El proceso puede repetirse sin riesgo.
- Los errores se informan sin detener el resto del proceso.

---

## 10. Buenas prácticas

- Probar primero con pocos ficheros.
- Revisar los resultados antes de cerrar.
- Conservar siempre los CSV originales.

---

## 11. Conclusión

Esta aplicación facilita la gestión de grandes volúmenes de datos ambientales,
garantizando calidad, trazabilidad y seguridad, y sirviendo como puente entre
el trabajo de campo y el análisis científico.
