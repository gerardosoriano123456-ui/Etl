# Extracción para el almacén de opiniones

**Actividad 1 · ETL en .NET 8**  
**Gerardo Soriano**

El Worker reúne respuestas de encuestas internas, reseñas web y comentarios sociales. Publica partes JSON de tamaño configurable y un manifiesto con conteos y SHA-256. El diseño analítico de destino es `analisis_opiniones_dw`, con `fact_opinion` y seis dimensiones.

## Probar en local

Requiere el SDK .NET 8, Python 3 y acceso a NuGet para la primera compilación. Ejecuta desde la raíz:

1. En una terminal, ejecuta `python fixtures/servidor.py`. El script crea la base SQLite de prueba y sirve los comentarios en localhost.
2. En otra terminal, ejecuta `dotnet run --project src`.
3. Abre `staging/<runId>/manifest.json`. Con la configuración de demostración deben aparecer 11 registros en seis partes. Los CSV de demostración proceden de los datos de ejemplo del proyecto anterior.
4. Detén la API con Ctrl+C al terminar.

## Usar el origen SQL Server

El adaptador consulta `dbo.resena_web` y `dbo.opinion` de la base transaccional `analisis_opiniones`. Usa una cuenta con permiso SELECT sobre ambas tablas y el nombre de servidor que figura en su certificado. Un ejemplo con autenticación integrada en PowerShell:

```powershell
$env:Sources__ReviewProvider='SQLServer'
$env:ConnectionStrings__Reviews='Server=SERVIDOR;Database=analisis_opiniones;Integrated Security=True;Encrypt=True;TrustServerCertificate=False'
dotnet run --project src
```

`Sources__SurveyCsv` apunta al CSV con los campos de `fixtures/respuesta_encuesta.csv`. `Sources__SocialUrl` recibe un arreglo JSON de objetos con `id` de tipo texto. Para usar un token Bearer, suministra `Sources__SocialToken` desde el gestor de secretos del entorno. La API remota requiere HTTPS.

## Leer el staging

El manifiesto describe las fuentes, los archivos, el número de registros y el hash de cada parte. Las partes contienen un arreglo de objetos con `originalId` y `payload` y pueden verificarse sin conexiones externas. Cada ejecución toma una nueva instantánea de las fuentes habilitadas en el código. Para producción aumenta `Staging__RowsPerFile` y limita el acceso al directorio.

El Worker escribe primero en `.pending-<id>` y confirma el lote renombrando la carpeta dentro del mismo directorio. Un error descarta la ejecución y devuelve 1, con 2 para una cancelación durante el ciclo. El consumidor debe ignorar los directorios temporales y comprobar los hashes con el manifiesto. Staging conserva respuestas individuales. Antes de cargar el DW, la transformación debe consolidar respuestas por encuesta y resolver duplicados mediante el canal y el identificador original de la opinión. Esa fase está representada en el diseño.

Incluye evidencias de compilación e integración con SQLite y la API local en `docs/evidencias`. SQL Server se configura mediante el adaptador, sujeto a una instancia con el modelo y el certificado disponibles.
