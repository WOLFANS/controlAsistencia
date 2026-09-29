\# Control de Asistencia



Solución de prueba técnica para el cálculo diario de asistencia mediante SQL Server y la importación de marcaciones desde un archivo CSV utilizando .NET 8.



\## Requisitos



\- SQL Server 2016 o superior.

\- .NET 8 SDK.

\- Instancia local de SQL Server.

\- Base de datos creada utilizando los scripts proporcionados con la prueba.



\## 1. T-SQL - Cálculo de horas



Los scripts desarrollados se encuentran en la carpeta `Sql`:



\- `Sql/01-sp\_calcula\_horas\_dia.sql`: procedimiento almacenado corregido.

\- `Sql/02-indexes.sql`: índice agregado para optimizar la búsqueda de marcaciones.



\### Correcciones realizadas



\- Se reemplazó `CONVERT(date, FechaHora)` por filtros mediante rangos de fecha, evitando aplicar funciones sobre `FechaHora`.

\- Se mantienen en el resultado los trabajadores activos de la empresa con turno asignado, aunque no tengan marcaciones.

\- Se utiliza `OUTER APPLY` para obtener la primera entrada y última salida sin excluir trabajadores ausentes.

\- Los turnos normales buscan su salida dentro del mismo día y los turnos nocturnos permiten buscarla durante el día siguiente.

\- Un trabajador sin entrada se informa como `Ausente`.

\- Si existe entrada pero no salida se informa como `Sin salida`.

\- Si faltan marcaciones necesarias para calcular las horas trabajadas, se informa `0.00`.

\- El atraso se limita a valores positivos; una llegada anticipada corresponde a 0 minutos.

\- Se mantiene el contrato de salida del procedimiento original.



\### Índice



Se agregó:



```sql

CREATE INDEX IX\_Marcacion\_Trabajador\_FechaHora

ON dbo.Marcacion (TrabajadorId, FechaHora)

INCLUDE (Tipo);

```



El índice permite buscar las marcaciones por trabajador y por rangos de `FechaHora`. Se verificó su comportamiento utilizando los datos adicionales del script de volumen proporcionado en la prueba.



\## 2. C# .NET 8 - Importador de marcaciones



El proyecto `controlAsistencia` contiene una aplicación de consola .NET 8 que procesa un archivo CSV con formato:



```text

rut;fecha\_hora;tipo;origen

12.345.678-5;2026-09-16 08:02:00;E;RELOJ

```



La aplicación:



\- Ignora líneas vacías.

\- Utiliza `;` como separador.

\- Valida fechas con formato `yyyy-MM-dd HH:mm:ss`.

\- Acepta únicamente tipos `E` y `S`.

\- Valida RUT chileno mediante módulo 11.

\- Normaliza el RUT antes de consultar la base de datos.

\- Comprueba que el trabajador exista, esté activo y pertenezca a la empresa 1.

\- Detecta duplicados dentro del archivo y marcaciones existentes en la base de datos.

\- Excluye `Origen` de la identidad utilizada para detectar duplicados.

\- Utiliza SQL parametrizado mediante ADO.NET.

\- Inserta las filas válidas utilizando una única transacción.

\- Las filas rechazadas no detienen el procesamiento del resto del archivo.



La identidad utilizada para detectar una marcación duplicada es:



```text

TrabajadorId + FechaHora + Tipo

```



\### Configuración



La cadena de conexión se encuentra en:



```text

controlAsistencia/appsettings.json

```



Ejemplo con autenticación integrada de Windows:



```json

{

&#x20; "ConnectionStrings": {

&#x20;   "DefaultConnection": "Server=SERVIDOR\_LOCAL;Database=PruebaGenera;Trusted\_Connection=True;TrustServerCertificate=True;"

&#x20; }

}

```



Se debe reemplazar `SERVIDOR\_LOCAL` por la instancia local correspondiente.



No se deben almacenar contraseñas reales en el repositorio.



\### Ejecución



Desde la raíz de la solución:



```powershell

dotnet run --project .\\controlAsistencia\\controlAsistencia.csproj -- ".\\Marcaciones\\marcaciones.csv"

```



Al finalizar se muestra un resumen con:



```text

Filas leídas: X

Aceptadas: X

Rechazadas: X

```



Las filas rechazadas se escriben en `rechazos.csv`, en el mismo directorio del archivo de entrada, utilizando:



```text

linea;rut;motivo

```



`linea` corresponde a la línea física original del CSV, considerando el encabezado como línea 1.



\### Idempotencia



Antes de insertar una marcación se comprueba si ya existe una combinación de `TrabajadorId`, `FechaHora` y `Tipo`.



La prueba manual con el CSV proporcionado obtuvo:



```text

Primera ejecución:

Filas leídas: 18

Aceptadas: 9

Rechazadas: 9



Segunda ejecución:

Filas leídas: 18

Aceptadas: 0

Rechazadas: 18

```



La tabla `Marcacion` tenía inicialmente 21 registros, aumentó a 30 después de la primera ejecución y permaneció en 30 después de procesar nuevamente el mismo archivo.



\### Pruebas unitarias



Ejecutar desde la raíz:



```powershell

dotnet test .\\controlAsistencia.Tests\\controlAsistencia.Tests.csproj

```



Resultado verificado durante el desarrollo:



```text

17 pruebas ejecutadas

17 pruebas correctas

```



Las pruebas cubren validación de RUT, tipos de marcación, detección de duplicados y generación del archivo de rechazos.



\## 3. Uso de IA

\- Herramientas: ChatGPT y GitHub Copilot.

\- Prompt útil 1: analizar el SP e identificar problemas con ausentes, turnos nocturnos y consultas no SARGables.

\- Prompt útil 2: implementar incrementalmente importación CSV, RUT, duplicados y transacción en .NET 8.

\- Se pidió mantener una solución simple, con ADO.NET parametrizado y sin frameworks innecesarios.

\- Descarté/corregí propuestas que usaban NVARCHAR donde el esquema SQL definía VARCHAR.

\- Verifiqué manualmente los tipos del esquema y los parámetros SQL utilizados.

\- Verifiqué el SP con ausentes, turnos nocturnos y datos de volumen.

\- Verifiqué build, 17 tests e idempotencia: primera importación 9 inserciones y segunda 0.

\- Tiempo real invertido: aproximadamente 8-10 horas de trabajo efectivo.

