# Teleprompter para Windows

Un teleprompter chiquito y liviano para usar durante videollamadas, presentaciones o grabaciones.
La ventana flota **siempre encima** de las demás, así que la podés poner justo debajo de la cámara y leer mirando casi a la lente.

**No hay que instalar nada.** Usa el .NET Framework que ya viene con Windows 10 y 11.

*English below.*

## Qué hace

- Ventana flotante, sin barra de título, con fondo oscuro levemente transparente y letra blanca grande.
- Siempre queda encima: aunque pongas PowerPoint o Google Slides en pantalla completa, vuelve a aparecer adelante. No se puede minimizar por accidente; solo se cierra con la ✕.
- Se arrastra desde cualquier parte del fondo y se cambia de tamaño desde los bordes.
- **Modo edición:** pegás o escribís el texto y tocás **Listo**.
- **Modo lectura:** el texto sube de forma suave y continua (sincronizado con el refresco de la pantalla).
- Una línea guía tenue en el tercio superior te marca qué línea leer, así tus ojos quedan cerca de la cámara.
- Las viñetas (`*` o `-`) y las sub-viñetas con sangría se muestran como listas de verdad.
- Controles discretos abajo, que se resaltan al pasar el mouse: reiniciar, play/pausa, velocidad, tamaño de letra, editar y cerrar.
- Recuerda el último texto, la velocidad, el tamaño de letra y la posición y el tamaño de la ventana.

## Cómo usarlo

1. Descargá el proyecto: botón verde **Code → Download ZIP**, y descomprimilo donde quieras.
2. Hacé doble clic en **`Abrir Teleprompter.bat`**.
   La primera vez tarda unos segundos porque lo compila con el compilador que trae Windows y crea `Teleprompter.exe`. Las siguientes veces abre al instante.
3. Si Windows muestra "Windows protegió su PC", tocá **Más información → Ejecutar de todas formas**.

### Atajos de teclado

| Tecla | Acción |
|---|---|
| Espacio | Play / pausa |
| ↑ / ↓ | Más / menos velocidad |
| Inicio | Volver al principio |
| Ctrl + Enter | "Listo" (en modo edición) |
| Ctrl + / Ctrl - | Letra más grande / más chica |
| Rueda del mouse | Mover el texto a mano |

Los atajos funcionan cuando la ventana del teleprompter tiene el foco: hacele un clic antes de usarlos.

## Privacidad

Tus ajustes y tu texto se guardan **solo en tu computadora**, en `teleprompter_config.txt`, al lado del programa. Nada se manda a internet.
Si hacés un fork o subís cambios, no subas ese archivo: el `.gitignore` ya lo excluye.

## Para desarrolladores

Todo el programa está en un solo archivo, `Teleprompter.cs` (C# 5, WinForms, .NET Framework 4.x). Para compilarlo a mano:

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /optimize+ /target:winexe /out:Teleprompter.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Teleprompter.cs
```

`Teleprompter.exe --selftest <carpeta>` corre una prueba automática: mide fps, velocidad y play/pausa, y guarda capturas y un `selftest.txt` en esa carpeta. Usa una configuración temporal, así que no toca la tuya.

¡Los pull requests son bienvenidos!

## Licencia

MIT: lo podés usar, copiar, modificar y compartir libremente. Ver [LICENSE](LICENSE).

---

## English

A tiny, always-on-top teleprompter for Windows, made to sit right under your webcam during calls and recordings. There is nothing to install: it compiles itself on first run with the .NET Framework that ships with Windows.

1. Download the ZIP and extract it.
2. Double-click **`Abrir Teleprompter.bat`**.
3. Paste your script, click **Listo**, then press **Space** to play or pause and **↑/↓** to change the speed.

Features: frameless semi-transparent window, smooth vsync scrolling, a reading guide line, bullet lists, and settings that are remembered between sessions. MIT licensed.
