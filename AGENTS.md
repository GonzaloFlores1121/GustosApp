# GustosApp backend

Estas instrucciones aplican a todo este repositorio backend.

## Arquitectura

- `src/GustosApp.Domain`: modelo de dominio, reglas de negocio e interfaces que no dependen de infraestructura.
- `src/GustosApp.Application`: casos de uso, servicios de aplicacion, validaciones y contratos.
- `src/GustosApp.Infraestructure`: Entity Framework Core, repositorios e integraciones externas.
- `src/GustosApp.API`: composicion, controladores, DTOs, middleware y hubs de SignalR.
- `test`: pruebas automatizadas con xUnit, Moq y FluentAssertions.

Respeta la direccion de dependencias existente. Domain no debe depender de Application, Infrastructure, API ni de SDKs de proveedores. Application puede depender de Domain, pero no de API ni de implementaciones de Infrastructure.

## Cambios y pruebas

- Todo cambio de logica en Domain o Application debe agregar o actualizar pruebas automatizadas.
- Todo bug corregido debe incluir una prueba de regresion que falle antes del arreglo y pase despues.
- No modifiques, debilites, ignores, comentes ni elimines pruebas solamente para hacerlas pasar.
- Cambia una prueba existente solo cuando el comportamiento requerido cambie deliberadamente; documenta ese cambio en el commit.
- Prefiere xUnit, Moq y FluentAssertions, siguiendo el estilo Arrange, Act, Assert usado por la solucion.
- Para cambios en endpoints, autenticacion, autorizacion, persistencia o integraciones, agrega pruebas de API o integracion cuando una prueba unitaria no cubra el riesgo.

## Implementacion

- Mantiene compatibilidad con .NET 8 y alinea las versiones de paquetes con el framework objetivo.
- Usa async/await de extremo a extremo para I/O y propaga `CancellationToken` cuando corresponda. No bloquees tareas con `.Result` o `.Wait()`.
- Registra dependencias mediante las extensiones de `IServiceCollection` existentes y elige lifetimes coherentes.
- Mantiene secretos y credenciales fuera de Git. Usa configuracion, variables de entorno o User Secrets, y conserva solo ejemplos sin valores sensibles.
- En EF Core, evita materializar antes de filtrar, usa consultas asincronas y aplica `AsNoTracking()` para lecturas cuando no se necesite tracking.
- Usa excepciones especificas o resultados de aplicacion para errores esperados; no dependas del texto de una excepcion para decidir el codigo HTTP.
- No introduzcas patrones, capas o abstracciones sin un problema concreto que los justifique.

## Idioma y nombres

- Mantiene en espanol los nombres propios del proyecto: clases, metodos, variables, DTOs, casos de uso, pruebas y archivos relacionados.
- Nombra las pruebas en espanol y expresa con claridad el escenario y el resultado esperado.
- Conserva en su idioma original los nombres impuestos por .NET, librerias, protocolos o proveedores, incluidas las firmas sobrescritas y las palabras tecnicas como HTTP, Redis, Firebase y SignalR.
- No traduzcas contratos HTTP, configuraciones externas, codigo generado ni APIs publicas existentes solo por uniformidad; cualquier cambio de contrato debe ser deliberado y estar cubierto por pruebas.
- Cuando modifiques codigo propio que mezcla idiomas, mejora los nombres de forma gradual y acotada, evitando renombrados masivos sin valor funcional.

## Verificacion y Git

Antes de finalizar una tarea que cambie codigo o configuracion:

```bash
dotnet build GustosApp.sln --configuration Release
dotnet test GustosApp.sln --configuration Release --no-build
```

- Informa cualquier prueba no descubierta, omitida o fallida; un exit code exitoso no reemplaza revisar el resumen.
- Conserva los workflows actuales: CI por push a `develop` y despliegue desde `master`, salvo pedido explicito del usuario.
- Revisa el estado de Git antes y despues de trabajar.
- No incluyas cambios previos o ajenos en un commit.
- Cuando el usuario autorice commits, crea commits descriptivos y enfocados. No hagas push salvo pedido explicito.
