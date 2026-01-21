# InventarioSilo API

API GraphQL para el inventario del Silo Tres Cruces. Esta carpeta contiene el backend ASP.NET Core (net9.0) listo para ejecutarse en Docker o Render.

## Variables de entorno requeridas

Configura las siguientes variables en tu entorno (local, Docker o Render). Usa `.env.example` como referencia.

- `MongoDbSettings__ConnectionString`
- `MongoDbSettings__DatabaseName`
- `JwtSettings__SecretKey` (32+ caracteres)
- `JwtSettings__Issuer`
- `JwtSettings__Audience`
- `JwtSettings__ExpirationMinutes`

## Ejecución local

```bash
cp .env.example .env # luego edita tus valores
 dotnet run
```

## Construir y probar la imagen Docker

```bash
docker build -t inventario-silo-api .
docker run --rm -p 8080:8080 \
  -e MongoDbSettings__ConnectionString=... \
  -e MongoDbSettings__DatabaseName=... \
  -e JwtSettings__SecretKey=... \
  -e JwtSettings__Issuer=InventarioSilo \
  -e JwtSettings__Audience=InventarioSiloUsers \
  inventario-silo-api
```

La API quedará disponible en `http://localhost:8080/graphql`.

## Despliegue en Render

1. Crea un servicio **Web Service** en Render seleccionando "Deploy an existing image" (Docker).
2. En "Start Command" Render usará el `ENTRYPOINT` del Dockerfile, así que deja el campo vacío.
3. Añade las variables de entorno listadas arriba en la sección *Environment* de Render.
4. Render expone el puerto `10000` por defecto, pero gracias a `ASPNETCORE_URLS=http://+:8080` la app escuchará en `8080`. Configura el "Port" de Render a `8080`.

Con esto, cualquier despliegue en Render se basará en la misma imagen que puedes construir localmente.
