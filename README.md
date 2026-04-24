# TerrainForger

<p align="center">
  <img src="Documentation~/images/TerrainForger.png" alt="TerrainForger" width="420" />
</p>

Unity Editor package para baixar dados GIS, exportar GeoTIFF para RAW/PNG e importar tiles de terreno.

## Dependencias

### Unity / UPM

- `com.unity.modules.terrain`: necessario para criar e manipular `Terrain`, `TerrainData` e `TerrainLayer`
- `com.unity.modules.imageconversion`: necessario para carregar previews raster com `Texture2D.LoadImage`

### Requisitos externos

- Unity `2020.3` ou superior
- QGIS instalado localmente para os fluxos de GeoTIFF e preview raster que usam ferramentas GDAL
- Chaves/configuracoes dos provedores quando voce usar downloads online:
  - OpenTopography
  - Mapbox
  - Google Maps Platform

## Seguranca das credenciais

- As credenciais do addon ficam salvas localmente em `UserSettings/TerrainDataServiceSettings.asset` do projeto Unity consumidor.
- Esse arquivo fica fora do package distribuido e o repositorio ignora `UserSettings/` no `.gitignore`.
- O package nao inclui API keys hardcoded: os campos de credenciais no codigo iniciam vazios e os valores sao lidos em tempo de execucao das settings locais do projeto.

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

- `TerrainForger/Get GIS Data`
- `TerrainForger/Geotiff2Raw Export`
- `TerrainForger/Import Tiles`

## Observacoes

- O package e editor-only.
- Os dados gerados continuam sendo salvos no projeto consumidor, em caminhos como `Assets/Terrain` e `Assets/Generated`.
- Sem QGIS/GDAL instalado, os recursos de importacao/exportacao GeoTIFF e alguns previews GIS nao funcionam.

## Caracteristicas atuais do addon

- `TerrainForger/TerrainForger`: janela raiz do addon, com acesso centralizado às ferramentas e às `Service Settings`.
- `TerrainForger/Service Settings`: configuração global de OpenTopography, Mapbox, Google Maps Platform e pasta local do QGIS/GDAL.
- Na primeira carga do addon no editor, TerrainForger abre a janela raiz e as configurações de serviço quando as credenciais/caminhos essenciais ainda não estão configurados.
- `TerrainForger/Get GIS Data`: carrega um source file local, refaz os bounds a partir do próprio raster e usa esses limites exatos para baixar DEM e SAT. Quando um source file válido está ativo, os `Map Bounds` ficam bloqueados para evitar inconsistência manual.
- `TerrainForger/Geotiff2Raw Export`: exporta tiles RAW 16-bit de DEM e tiles PNG de satélite usando a mesma grade, nomes e área geográfica.
- `TerrainForger/Import Tiles`: importa RAWs como `TerrainData` e aplica PNGs de satélite como `TerrainLayer` mantendo a resolução real do PNG no importador da Unity, evitando o downscale padrão de 4096 para 2048.
- As janelas de ferramenta exibem tooltips nos controles principais, possuem uma área visual de log do fluxo executado e mantêm `Save Tool Settings` / `Reset Tool Settings` no rodapé.
- Os previews de Source, DEM e SAT devem representar a mesma área quando o fluxo é conduzido a partir de source file, pois o source passa a ser a autoridade dos bounds.
