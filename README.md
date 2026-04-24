# TerrainForger

<p align="center">
  <img src="Documentation~/images/TerrainForger.png" alt="TerrainForger" width="420" />
</p>

Unity Editor package para baixar dados GIS, exportar GeoTIFF para RAW/PNG e importar tiles de terreno.

## Instalacao

### Via Git URL

No `Packages/manifest.json` do projeto Unity:

```json
{
  "dependencies": {
    "com.arantes83.terrainforger": "https://github.com/Arantes83/terrainforger.git"
  }
}
```

### Via Package Manager

Abra `Window > Package Manager`, escolha `Add package from git URL...` e informe a URL do repositorio.

### Via pasta local

Se estiver desenvolvendo localmente, use `Add package from disk...` e selecione o `package.json` deste repositorio.

## Estrutura do package

- `package.json`: metadados do UPM package
- `Editor`: scripts editor-only do addon
- `Documentation~`: documentacao do package

## Menus criados no Unity

- `Tools/TerrainForger/Get GIS Data`
- `Tools/TerrainForger/Geotiff2Raw Export`
- `Tools/TerrainForger/Import Tiles`

## Observacoes

- O package e editor-only.
- Os dados gerados continuam sendo salvos no projeto consumidor, em caminhos como `Assets/Terrain` e `Assets/Generated`.
