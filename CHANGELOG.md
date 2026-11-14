# Cambios

Todas las versiones de Fortnite Chest Tracker (FCT). Sigue el [versionado semántico](https://semver.org/lang/es/): **MAJOR** cuando algo deja de ser compatible, **MINOR** para funciones nuevas, **PATCH** para arreglos.

## [Sin publicar]

- **Restaurar la copia de seguridad** desde Configuración → Copia de seguridad: muestra la última copia (la que se hace al restablecer las estadísticas) y la devuelve a tu historial con sus partidas, tesoros y cofres de antes de FCT. Se suma: las partidas que ya tienes, también las jugadas después, se quedan. Con varias copias, eliges cuál. La tarjeta del overlay para OBS ocupa menos.
- **Las copias con otra carpeta de datos (`--data`, pruebas) quedan aisladas del FCT normal**: desinstalarlas solo borra esa copia; al actualizarse se reinician con su carpeta; no avisan al proceso en espera del FCT normal, y abren su página y su overlay de OBS en otro puerto (desde el 7473) para no quitarle el suyo.
- **Menú del icono de la bandeja renovado**: un icono dorado por opción (el logo para «Abrir FCT»), agrupado con separadores dorados finos y un marco neutro, también en el desplegable de idioma.
- **«Desde el…» del total**: si el total incluye cofres de antes de FCT, cuenta desde el inicio de la temporada (1 de noviembre de 2026); si no, desde tu primera partida guardada.
- **Tras actualizarse, FCT vuelve a abrir su ventana** si la tenías a la vista o si Fortnite no está en pantalla (minimizado o tapado). Solo se queda en la bandeja si estabas jugando con la ventana guardada.
- **La ventana ya no sale cortada** al abrirla desde la bandeja tras arrancar sola: la tarjeta del equipo cabe entera.
- El aviso «FCT ya estaba abierto» al abrirlo por segunda vez dura ahora 5 segundos.
## [5.6.0] - 2026-11-14

- **Detalles de la última partida en la ventana**: tu Top (o Victoria), eliminaciones, tesoros obtenidos y cuánto duró, a su tamaño normal (antes el texto se encogía para caber). En Estadísticas, «Últimas partidas» muestra también la duración.
- **Tiempos con su unidad en todas partes**: duraciones como «18 min» y momentos de la partida como «3:51 min».
- **Gráfico de la semana más legible**: la escala y los días en un gris más claro.
- **README**: el principal pasa a estar en inglés (el español sigue en [README.es.md](README.es.md)), con botones para cambiar de idioma arriba; nueva sección sobre la exactitud (los cofres se cuentan todos; algún artilugio de tesoro puede escaparse) y capturas al día.
- **Los cofres enterrados cuentan como cofres normales** en las estadísticas (Cofres, origen de cada tesoro y «Dónde consigo mis tesoros»). Esa última lista muestra siempre los cofres que dan tesoros, también los cofres de espíritu aunque aún no te hayan dado ninguno.
- **Tachar premios en las semanas** (Estadísticas → Tesoros → Ver todas las semanas): toca un premio para tacharlo o destacharlo. Los que consigues se tachan solos, y siempre puedes cambiarlos a mano; se guardan con tu configuración. El botón de las semanas y los premios de «Todas las semanas» van ahora en gris: solo el Pirate Shop Access se ilumina si lo consigues.

## [5.5.0] - 2026-11-14

- **Abrir FCT a mano siempre muestra su ventana**, aunque Fortnite esté abierto (minimizado o en segundo plano). Solo arranca escondido en la bandeja cuando lo abre Fortnite o se reinicia tras una actualización.
- **Contar también partidas clasificatorias** (desactivado por defecto), en Configuración de la app y de la web.
- **Los ajustes se comparten entre la app y la web**: compañeros, sumar sus cofres, clasificatorias, cajas de munición y de comida, cañones de botín e idioma. Lo que cambies en un sitio cambia en el otro; la ventana de Configuración y una página abierta se actualizan solas.
- **Solo cuentan los modos del evento**: Battle Royale con construcción y Construcción Cero, en solitario, dúos, tríos o escuadrones. Las partidas de otros modos (Clasificatoria, Recarga, Blitz, modos temporales, Creativo…) se guardan igual pero no suman cofres, partidas ni tesoros, ni en la ventana, ni en el overlay, ni en las estadísticas.
- **Tesoros de las semanas 3 a 5** (según las filtraciones, marcados «sin confirmar» hasta que salgan en el juego): Miku, Kasane Teto y Capitán X de oro macizo con sus mochilas, pistas, pantallas de carga y coches dorados. Las semanas cambian a la hora exacta (domingo 21:00 ET, lunes 03:00 en España), no por días.

## [5.4.0] - 2026-11-14

- **Overlay sin cursor en partida**: el overlay ya no hace aparecer ni parpadear el puntero sobre Fortnite. Deja pasar el ratón al juego y solo lo retiene cuando el puntero está libre (menús, Esc, mapa, escritorio) encima de su candado y su ✕, o de todo él si está desbloqueado; así sus clics no llegan al juego ni a la ventana de debajo, también en modo ventana. El candado y la ✕ se iluminan al pasar, y el overlay se pinta de una vez, sin destellos.
- **Overlay reactivo ante ventanas**: se oculta cuando Fortnite queda fuera de la vista aunque Windows no lo marque como minimizado: Win + D, o ventanas que lo tapen casi entero (el 80 %), una o varias juntas, tengan o no el foco y se hayan abierto desde la pantalla que sea. La barra de tareas y sus menús, el selector de Alt + Tab, las capas transparentes (como las de la tarjeta gráfica), las ventanas «siempre encima» y las herramientas de captura (Win + Mayús + S, Recortes, ShareX, Lightshot…) no cuentan: hacer una captura ya no lo oculta. Reacciona también cuando una ventana aparece, se va o se mueve.

## [5.3.0] - 2026-11-14

- **Aviso de pantalla completa exclusiva**: si Fortnite está en Pantalla completa (exclusiva), donde ninguna ventana puede verse encima, FCT lo avisa con una notificación del sistema y con un mensaje en la ventana de la aplicación.
- **Limpieza de versiones anteriores**: si abres una versión nueva con una anterior aún abierta, FCT cierra la vieja y ocupa su lugar en vez de decir que ya está abierto. Además, la primera vez que se abre cada versión borra lo que dejaron las anteriores (desde la 1.0.0): copias antiguas de FCT en marcha y sus exes (donde apuntaba el inicio con Fortnite, el último que se usó o junto al nuevo, como un `FCT (1).exe` descargado), restos de actualizaciones, entradas de inicio con otros nombres y archivos temporales. Solo toca exes que sean de FCT y de una versión anterior; tus datos, copias de seguridad y la carpeta de la 1.0.0 no se tocan.
- **Partidas de antes de FCT en la ventana**: si al empezar o al recalcular eliges contar las partidas antiguas (con el total de la temporada buscado o escrito a mano), la cifra de Partidas suma las de esa temporada que FCT no tiene en repeticiones, igual que el total de cofres suma los estimados. Con «empezar de cero» no se suma nada.
- **Pantalla de tesoros más fiable**: los fuegos artificiales dorados y los localizadores se reconocen aunque la tarjeta aún esté apareciendo o brillando; las capturas empiezan antes y son más seguidas (la pantalla a veces dura solo 2 o 3 segundos) y se combinan tarjeta a tarjeta. Como el juego junta en una sola tarjeta los tesoros iguales, cuando hay menos tarjetas que tesoros se nombra con seguridad el primer artilugio de la partida y el resto queda como «Artilugio».
- **Estadísticas afinadas**: el aviso de que FCT está cerrado sale en el menú lateral, debajo de la última sección; en Compañeros la media también se muestra como mínimo (≥); en «Tesoros disponibles actualmente», la fila «Todas las semanas» ya no lleva un contador por artilugio (los totales siguen arriba). En Resumen, «Últimas partidas» muestra 5 (caben enteras en una pantalla 1920×1080 sin barra de desplazamiento), «Últimos tesoros» se desplaza fila a fila sin dejar ninguna cortada y las columnas guardan la misma separación que las tarjetas de arriba.

## [5.2.1] - 2026-11-13

- **Overlay**: eliminación de los atajos de teclado globales; la visibilidad y bloqueo se gestionan exclusivamente desde el botón nativo de la ventana principal y el icono de candado.
- **Avisos legales depurados**: delimitación precisa entre [NOTICE.md](NOTICE.md) (licencia GPL-3 y términos de atribución), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) (librerías externas, servicios y marcas) y la política de privacidad de [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md).
- **Documentación renovada**: los README en español e inglés completamente reestructurados orientados al jugador, con nueva tabla visual de funciones, capturas actualizadas con datos demo, banner oficial en negro y oro, botón de descarga dorado y formato pulido.
- Configuración de compilación afinada en `Directory.Build.props` (aislamiento de advertencias de dependencias externas).

## [5.2.0] - 2026-11-13

- **Reconstrucción modular completa de la interfaz de usuario**:
  - Generador de la página de estadísticas dividido en una clase especializada por cada pestaña (`ResumenTab`, `CofresTab`, `TesorosTab`, `PartidasTab`, `CompanerosTab`, `ConfiguracionTab`). La página incluye sus estilos y scripts en recursos propios dentro del ejecutable.
  - Controles de interfaz desacoplados en archivos individuales (`FitLabel`, `AppSwitch`, `AppPill`, `AppField`, `ProportionalLayout`, `GoldButton`, etc.).
  - Overlay en el juego descompuesto en componentes independientes (`OverlayModel`, `OverlayScale`, `OverlayLayout`, `OverlayPainter`, `GameWindows`).
  - Capas de renderizado del tema visual (`Theme`), marco de ventana (`WindowChrome`) y curva del gráfico semanal (`DailyChart`).
  - Ventana principal (`MainForm`) y ventana de configuración (`SettingsForm`) estructuradas por módulos de estado, acciones y tarjetas.
  - Nuevo icono oficial de la aplicación.
  - Selección de repeticiones legibles como paso propio del Tracker y `StandbyWatcher` en su propia clase.

## [5.1.0] - 2026-11-13

- **Reescritura del análisis y monitorización de replays**:
  - `Tracker` dividido por responsabilidades: vigilancia de carpeta, lectura en segundo plano, seguimiento de partida en curso y salida de consola.
  - Análisis de repeticiones desacoplado en `Analysis/Replay` y modelos fuertemente tipados en `ReplayModels`, sobre la librería base con parches mínimos documentados.
  - `AppMonitor` reescrito como coordinador: gestión separada de ajustes, cifras para la ventana, cálculo de partidas previas, detección del jugador activo y pantalla de tesoros.
  - Arranque (`Program.cs`) modularizado: parser de opciones CLI, herramientas de consola, detección de instancia única y primer uso como unidades segregadas.

## [5.0.0] - 2026-11-13

Cambio mayor en el licenciamiento, compatibilidad heredada y arquitectura base:

- **Licencia: GNU GPL versión 3** (las versiones anteriores hasta la 4.4.1 continúan bajo MIT), incorporando condiciones de atribución expresa y distribución con código fuente abierto ([NOTICE.md](NOTICE.md)). Nuevo diálogo y opción **Acerca de y licencia** en el menú contextual del icono de notificación. ChestCounter queda reconocido como el proyecto original del que partió la idea.
- **Ruptura de compatibilidad heredada**: se retira la migración automática de la carpeta de datos de la v1.0.0 (`%APPDATA%\ChestCounter`). Los datos de versiones actuales se mantienen en `%APPDATA%\FCT`.
- **Reescritura de la capa de datos y servidor local**:
  - Modelos de datos (`MatchRecord`, `ChestRecord`, `TeammateRecord`, `TreasureRecord`) ordenados y consistentes con el formato `data.json`.
  - Capas `Store` y `Config` desacopladas con lectura y escritura atómica y segura mediante `JsonFile`.
  - Tipos de contenedor, artilugios y eventos de partida definidos en tablas de reglas estáticas; lectura de cabecera de repetición en un único recorrido.
  - Servidor local HTTP simplificado (`LocalHttp`) con enrutamiento declarativo para OBS y estadísticas.
  - Utilidades del sistema reorganizadas (`Log`, consola con API `Show/Erase`, modo de eficiencia).

## [4.6.0] - 2026-11-13

- **Desinstalador integrado**: opción de desinstalación limpia accesible desde Configuración y mediante CLI con `FCT.exe --uninstall`. Cierra el programa y elimina el ejecutable, la tarea programada, las entradas de registro y residuos de actualización, conservando de forma segura los datos del usuario en `%APPDATA%\FCT`.
- **Configuración depurada**: interfaz sin el pie redundante de «Código fuente», columnas con altura simétrica, selector de idioma centrado con flecha desplegable independiente y explicaciones detalladas para actualizaciones y OBS.
- **Avisos legales y licencias**: incorporación de [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) detallando licencias de terceros, orígenes, marcas registradas de Epic Games y servicios de API; comando `FCT.exe --licenses` para inspección en terminal.

## [4.5.0] - 2026-11-13

- **Abrir con Fortnite sin procesos en segundo plano**: sustitución del proceso residente en el inicio de Windows por una tarea programada del usuario (sin privilegios de administrador) activada por los eventos del controlador del antitrampas (`EasyAntiCheat_EOSSys` / `BEDaisy`).
- **Lanzador inteligente**: arranque ultraligero que verifica procesos del juego y se cierra de inmediato (~0,1 s) si el evento procede de otro título que use el mismo antitrampas.
- **Nuevo repositorio**: transición completa al nombre oficial **Fortnite-Chest-Tracker**, actualizando enlaces de descarga, badges, telemetría y documentación.

## [4.4.1] - 2026-11-13

- Corregido: una repetición muy corta justo después de una partida (entrar y salir) se quedaba también con la pantalla de tesoros de esa partida, y su tesoro se contaba dos veces. Al abrir FCT se corrige solo en los datos guardados.
- **Overlay y tarjetas**: sin la raya que dejaba el velo dorado; ajuste de espaciado entre Hoy, Media y Tesoros, con líneas doradas finas entre las columnas de cifras y bloque de cifras centrado.

## [4.4.0] - 2026-11-12

- **Varios jugadores en un PC**: FCT sigue al que juega (al empezar o terminar una partida de otra cuenta cambia a su perfil y avisa «Ahora juega X»); la partida en directo de uno ya no se suma al overlay ni a las cifras del otro; «Cofres antes de FCT» es de cada jugador (el estimado de versiones anteriores pasa a su perfil); al cambiar de perfil, la página de estadísticas cambia al momento. El historial de cada cuenta se conserva como siempre.
- **Un solo FCT a la vez**, sea cual sea la carpeta de datos (antes, uno por carpeta: podían salir dos overlays). Al abrirlo otra vez no se abre otro: el que ya está saca su ventana con el aviso «FCT ya estaba abierto»; si aún está en la bienvenida, el segundo lo avisa con su propio mensaje. Abierto por Fortnite estando ya abierto, se cierra sin decir nada.
- **Overlay con el estilo de la app** (igual de sencillo): negro con el velo dorado de las tarjetas, borde y franja de oro (menta en directo), «◆ COFRES», cifras grandes con el degradado de oro, cápsula dorada para la partida, columnas centradas y la gema dorada para los tesoros. La versión para OBS, igual.

## [4.3.0] - 2026-11-12

- **Tesoros que la repetición no ve** (conseguidos después de morir): la pantalla de fin de partida los suma siempre, con su nombre si se lee y sin él si no; una tarjeta ilegible ya no hace perder la pantalla entera, y también se suman los tesoros destacados. Cada pantalla se asocia solo a su partida: antes, una partida sin pantalla leída podía quedarse con la de la siguiente.
- **Pantalla de tesoros de fin de partida**: ahora se leen bien las tarjetas cuyo icono las cruza o las llena (Acelerador de extracción, fuegos artificiales) y ya no se confunde el rótulo dorado de «Victoria» con una tarjeta. Comprobado con capturas reales: acelerador, localizador de la suerte + fuegos, localizador de espíritus, fuegos y extractor portátil. Se capturan más veces (durante 9 s) para no llegar antes de que aparezcan todas.
- **Overlay más fiable**: solo cuenta una ventana de Fortnite que esté de verdad en pantalla (con tamaño real, dentro de un monitor y sin ocultar por Windows), así no aparece con Fortnite minimizado; comprueba cada 2 s desde su propio hilo, así se va al cerrar Fortnite aunque FCT esté leyendo repeticiones; y apagado a mano ya no se vuelve a encender al minimizar y restaurar Fortnite (solo al abrir un Fortnite nuevo).
- **Overlay y gráficos nítidos**: textos más nítidos (ningún texto por debajo de 9 px) y a la escala del monitor donde está, también al moverlo a otra pantalla. Gráfico semanal con textos nítidos y la cifra siempre encima de su punto. Ventana: el botón Estadísticas es como los demás, con las letras en oro; las cifras de las seis tarjetas, algo más arriba.
- **Buscar partidas (fortnite-api.com)**: una cuenta sin partidas esta temporada ya no sale como «no encontramos a nadie con ese nombre» (son 0 partidas); mensajes propios para demasiadas búsquedas a la vez (límite de 180 por minuto) y clave no válida. Probado con distintos nombres: mayúsculas, espacios, perfiles privados, nombres inexistentes y caracteres raros.
- Configuración → Recalcular: sin ninguna partida guardada, la primera opción es «Empezar de cero» (como en la primera apertura). Comprobado que desde Configuración las partidas guardadas cuentan igual que en la primera apertura.
- Textos: «Calcular partidas previas» en la primera apertura y el diálogo; «Recalcular» en Configuración. Estadísticas vuelve a ir en blanco, como los otros botones.
- **Firma del ejecutable preparada** (SignPath, gratis para código abierto): [política de firma](CODE_SIGNING_POLICY.md) y paso de firma en el flujo de GitHub, que se activa en cuanto SignPath esté configurado. Hasta entonces Windows puede avisar al abrir el exe (README: *Más información → Ejecutar de todas formas*).
- Las releases ya no llevan la copia `ChestCounter.exe`: las versiones de antes del cambio de nombre ya no se actualizan solas (hay que descargar `FCT.exe` una vez).
## [4.2.1] - 2026-11-12

- Primer uso: la opción sin estimado pasa a llamarse **Empezar de cero** cuando no se cuentan partidas guardadas (no hay repeticiones o se eligió *Solo las nuevas*).
- Documentación (`README.md`, `README.en.md`) y versión de compilación (`4.2.1`) actualizadas.

## [4.2.0] - 2026-11-12

- **Esta semana** con el gráfico de la página: curva dorada con relleno, escala, media discontinua y puntos (hoy en blanco). Tarjeta principal con las cifras centradas.
- Configuración → Cofres antes de FCT: cifra entre `−` y `+`, y el botón **Recalcular** (abre el mismo diálogo de la primera apertura, «Calcular partidas previas»).
- **Abrir con Fortnite** activado por defecto (también una vez en instalaciones previas al actualizar; luego se respeta lo que elijas). Una copia de prueba (`--data`) ya no cambia el inicio de Windows al activarlo.

## [4.1.0] - 2026-11-12

- **Sin logo en la interfaz** (solo en la barra de título): ventana principal con la estructura clásica (cofres y última partida, sus detalles, la semana; seis cifras en rejilla y los botones) sobre el diseño negro y oro. El perfil pasa a mostrarse como texto.
- **Textos más cercanos y cortos** en la bienvenida, el aviso de versión nueva, «Partidas de antes» y los avisos de Configuración, con iconos con sentido; restablecer estadísticas y otros avisos dejan de usar cuadros grises de Windows.
- **Partidas de antes**: solo se pide el nombre de Epic y se buscan las partidas; `fortnite.gg` y el número a mano solo aparecen si no se encuentran o el perfil es privado. Arreglados los recuadros oscuros de los campos de texto.
- **Por defecto activados**: overlay y compañeros (en ventana y página).

## [4.0.0] - 2026-11-12

Cambio incompatible en la interfaz de usuario: rediseño visual completo de la aplicación de escritorio en negro y oro.

- **Nuevo diseño de la app**, a juego con la página de estadísticas: negro y oro, tarjetas con velo dorado, los mismos iconos SVG y estética unificada en ventana principal, Configuración, bienvenida y diálogos.
- Corregido: al aparecer un aviso o la tarjeta de equipo, la ventana podía mostrar el error `Parameter is not valid` por fuentes liberadas al reescalar.

## [3.7.0] - 2026-11-12

- **Overlay**: solo se ve con Fortnite abierto (sin minimizar) en alguna pantalla; entonces se puede poner en cualquier pantalla y sitio, sin salirse por los bordes al arrastrarlo. Se enciende solo al abrir Fortnite y se oculta al cerrarlo aunque la ventana de FCT siga visible.
- **Abrir y cerrar**: con Fortnite abierto, FCT arranca en segundo plano con el overlay; sin Fortnite, abre la ventana. La `X` de la ventana la deja en segundo plano si Fortnite está abierto (si no, cierra); la `✕` del overlay cierra FCT. En segundo plano se cierra solo al cerrar Fortnite, o desde *Salir* en el área de notificación.

## [3.6.0] - 2026-11-12

- **Partidas de antes** (primer uso y Configuración → Cofres antes de FCT → *Estimar…*): *solo repeticiones guardadas* o *repeticiones + estimación*. Estimado = media × (partidas de la temporada actual − repeticiones de esta temporada que FCT ya cuenta). Partidas de la temporada buscadas en `fortnite-api.com` (si la compilación lleva clave) o escritas a mano desde `fortnite.gg` (con aviso si el perfil es privado); media ponderada entre repeticiones guardadas y partidas nuevas (5 por defecto, 10 recomendado) o manual.
- Ventana principal: los avisos y actualizaciones se muestran en una franja propia bajo la ventana sin tapar los botones; avisa también mientras lee una tanda de replays (`Leyendo tus replays… 3 / 40`).
- Primer uso: cerrar cualquiera de las ventanas iniciales cierra la app sin guardar nada; al volver a abrirla empieza de nuevo.

## [3.5.0] - 2026-11-12

- La página de estadísticas (antes «Informe» / «Resumen») pasa a llamarse **Estadísticas** en toda la aplicación, la consola y la documentación; ahora se sirve en `http://localhost:7373/estadisticas` (`#resumen`) y se guarda como `estadisticas.html` (el antiguo `report.html` se borra).
- **Idioma bidireccional**: cambiar el idioma en la app regenera y recarga automáticamente la página de Estadísticas abierta con el nuevo idioma, y cambiarlo desde la página (con el bloque de idioma centrado y estilizado como el resto de ajustes) actualiza y reinicia la app automáticamente.
- Resumen: alineados los encabezados y los bordes superior e inferior entre «Últimas partidas» y la columna derecha («Últimos tesoros» y el resumen de compañeros), con barras de desplazamiento internas en tarjetas largas.
- Cifras: «tuyos por partida» y «del equipo por partida» de Compañeros pasan a «en equipo», y la línea del gráfico indica «media por día jugado».
- Tesoros: en «Todas las semanas», se quita la ficha con el recuento total de artilugios.
- `build.bat`: al terminar de compilar `dist\FCT.exe`, abre directamente la aplicación en lugar de abrir la carpeta en el explorador.

## [3.4.1] - 2026-11-12

- Actualizaciones: el aviso de descarga, reinicio o error tiene prioridad sobre el botón de actualizar (antes quedaba oculto detrás del botón); los errores se registran también en `errors.log`.
- Actualizador: reintentos breves al sustituir el `.exe` (por bloqueos puntuales del antivirus) y restauración automática del ejecutable anterior si falla el intercambio.

## [3.4.0] - 2026-11-12

- Actualizaciones: se buscan al iniciar y cada 30 minutos mientras FCT está abierto. Cada release incluye también `ChestCounter.exe` (el mismo exe) para que las versiones de antes del cambio de nombre se actualicen.
- Primer uso: si no es la última versión, pregunta si descargarla (si no, las actualizaciones quedan en *Preguntar*); «Abrir con Fortnite» viene activado.
- «Informe» pasa a llamarse **Resumen** y «Ajustes» **Configuración**, en la app y en la página.
- El resumen tiene su propia configuración (idioma sin reiniciar, compañeros, qué cuenta como cofre): solo cambia la página; la ventana y el overlay usan la de la app. «Abrir con Fortnite» y «Actualizaciones» quedan solo en la app.
- Artilugios con su nombre en español cuando la app va en español (Extractor portátil, Localizador de la suerte, Localizador de espíritus, Acelerador de extracción, Fuegos artificiales dorados).
- Resumen: seis últimas partidas (las dos columnas acaban a la vez); «Cofres del equipo abiertos por tus compañeros» en vez de «Aportan tus compañeros»; columnas numéricas centradas en todas las tablas; Configuración rediseñada.

## [3.3.0] - 2026-11-12

- Informe: pestaña **Ajustes** (idioma, abrir con Fortnite, abrir el informe al iniciar, actualizaciones, compañeros, qué contar como cofre, versión y enlaces a GitHub). El informe lo sirve FCT en `http://localhost:7373/informe` para poder cambiarlos desde la página; si FCT se cierra, la página lo avisa.
- La versión ya no aparece en la app (ni en el título): solo en Ajustes del informe.
- Informe más dorado e iconos nuevos (cofre, tesoros, partidas…); en Últimos tesoros, el icono de cada objeto cuando se sabe cuál es.
- Compañeros en una sola página, sin menú: tabla de todos tus compañeros que se ordena por cualquier columna (partidas, victorias, cofres abiertos, media…) y top 5 de más cofres abiertos y de récord. «Cofres vistos» pasa a «cofres abiertos».
- Resumen: los compañeros siempre a la vista, sin desplazar.
- Tesoros: «Caza del tesoro» pasa a «Tesoros disponibles actualmente» y «De dónde vienen los tesoros» a «Dónde consigo mis tesoros»; «Ver todas las semanas» abre las cinco semanas en una fila y ya no se cierra sola.
- Cofres: la tabla por modo de juego muestra los tesoros de cada modo en vez de las victorias.
- Se quita el aviso «tesoros posteriores no visibles» tras una eliminación: la pantalla de fin de partida ya los recupera.

## [3.2.0] - 2026-11-12

- Idiomas: la app y el informe quedan en español (de España) e inglés.
- Compañeros: clasificación unificada con selector (más cofres vistos, mejor media, récord, victorias juntos y la tabla con todos).
- Textos revisados: tono neutro y español de España («Te damos la bienvenida», «Haz clic», «destruidos»).

## [3.1.0] - 2026-11-12

- Informe rediseñado en negro y dorado: menú lateral con el logo, cabecera en cada pestaña, tarjetas con icono, gráfico de curva de 30 días, **últimos tesoros** y resumen de compañeros en el Resumen; sin pie de página ni estado «En directo».
- Informe: en un PC cada pestaña cabe en la pantalla; las listas largas se desplazan dentro de su recuadro. En Compañeros, «por partida», «con quién más juegas» y «todos tus compañeros» van en una sección con selector.
- Informe: la caza del tesoro está en Tesoros (premios de la semana como fichas, el resto plegado); textos unificados (mayúscula inicial, versalitas solo en etiquetas) y «Victoria» en vez de «Victoria Real»; se quita la nota larga sobre los cofres de los compañeros.

## [3.0.0] - 2026-11-12

Cambio incompatible: requiere Windows 10 2004 o posterior (captura de ventanas de Windows).

- Pantalla de tesoros de fin de partida: FCT captura solo la ventana del juego (aunque esté tapada), reconoce los iconos, borra la imagen y pone nombre a cada artilugio; añade los tesoros que el replay no vio (tras tu eliminación) con hora y jugador desconocidos.

## [2.2.0] - 2026-11-12

- Sin «sesiones»: la ventana, el overlay y el informe muestran lo de **hoy** (cofres, partidas y media por partida de hoy).
- Ventana: el gráfico es la semana actual, de lunes a domingo.
- Informe: logo de la app, pestañas más grandes con iconos, estado «En directo», textos unificados (mayúscula inicial) y «Victoria» en vez de «Victoria Real».
- Estructura del proyecto: la aplicación en `src/` (`FCT.csproj`) con el código por carpetas (`App`, `Analysis`, `Data`, `Monitoring`, `Overlay`, `Reporting`, `UI`, `Localization`), librería de replays en `external/`, solución `FCT.slnx`.

## [2.1.0] - 2026-11-12

- Informe: sin pie de página; la caza del tesoro pasa al Resumen (premios de la semana como fichas y el resto plegado en «Ver todas las semanas»).
- Resumen: 5 últimas partidas en vez de 6 y gráfico de 30 días ajustado.
- Workflow: subir una etiqueta solo compila `FCT.exe`; las releases se publican a mano en GitHub y se puede adjuntar `FCT.exe` a cualquier etiqueta desde Actions.

## [2.0.2] - 2026-11-12

- Sin aviso al volver a leer los replays tras un cambio del análisis.

## [2.0.1] - 2026-11-12

- Los avisos de la ventana se quitan a los 20 s y vuelven los botones Overlay, Informe y Ajustes.

## [2.0.0] - 2026-11-12

Cambio incompatible: los datos pasan a `%APPDATA%\FCT` y la v1.0.0 ya no puede actualizarse sola (hay que descargar `FCT.exe` una vez).

- Al abrir la versión nueva, el historial de la v1.0.0 se traslada solo (si la v1.0.0 está abierta, pide cerrarla).
- Fuera los nombres y el código de versiones anteriores.

## [1.5.0] - 2026-11-12

- Overlay: botón de cerrar a la derecha del candado (cierra la aplicación).

## [1.4.0] - 2026-11-12

- Informe en negro y dorado.
- Tesoros destacados con su nombre y su tipo, sin ids internos; catálogo de premios de la caza del tesoro por semana.
- Nombres de los cofres del juego (Cofre de espíritu, Cofre raro…).

## [1.3.0] - 2026-11-12

- Nuevo nombre: Fortnite Chest Tracker (FCT); el exe pasa a ser `FCT.exe`.
- Actualizar y "Abrir con Fortnite" funcionan aunque el exe tenga otro nombre.

## [1.2.0] - 2026-11-12

- Al abrir solo se ve el overlay; la ventana queda en el área de notificación (flecha ^) y se recupera con un clic o abriendo el programa otra vez.

## [1.1.0] - 2026-11-12

- Partidas: se registra la eliminación definitiva; el informe avisa de que los tesoros del escuadrón posteriores no quedan en el replay.

## [1.0.0] - 2026-11-11

- Primera versión publicada.

[Sin publicar]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v5.6.0...HEAD
[5.6.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v5.5.0...v5.6.0
[5.5.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v5.4.0...v5.5.0
[5.4.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v5.3.0...v5.4.0
[5.3.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v5.2.1...v5.3.0
[5.2.1]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v5.2.0...v5.2.1
[5.2.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v5.1.0...v5.2.0
[5.1.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v5.0.0...v5.1.0
[5.0.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v4.6.0...v5.0.0
[4.6.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v4.5.0...v4.6.0
[4.5.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v4.4.1...v4.5.0
[4.4.1]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v4.4.0...v4.4.1
[4.4.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v4.3.0...v4.4.0
[4.3.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v4.2.1...v4.3.0
[4.2.1]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v4.2.0...v4.2.1
[4.2.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v4.1.0...v4.2.0
[4.1.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v4.0.0...v4.1.0
[4.0.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v3.7.0...v4.0.0
[3.7.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v3.6.0...v3.7.0
[3.6.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v3.5.0...v3.6.0
[3.5.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v3.4.1...v3.5.0
[3.4.1]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v3.4.0...v3.4.1
[3.4.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v3.3.0...v3.4.0
[3.3.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v3.2.0...v3.3.0
[3.2.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v3.1.0...v3.2.0
[3.1.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v2.2.0...v3.0.0
[2.2.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v2.1.0...v2.2.0
[2.1.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v2.0.2...v2.1.0
[2.0.2]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v1.5.0...v2.0.0
[1.5.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/adrc4-ua/Fortnite-Chest-Tracker/releases/tag/v1.0.0
