# Publicacao do Package

Como houve uma tentativa interrompida de `git init`, remova a pasta `.git` parcial antes de comecar:

```powershell
Remove-Item -LiteralPath .git -Recurse -Force
```

Depois execute:

```powershell
git init
git add .
git commit -m "Initial commit"
```

Se voce tiver o GitHub CLI instalado:

```powershell
gh repo create terrainforger --public --source . --remote origin --push
```

Se preferir criar pelo site do GitHub:

1. Crie um repositorio vazio no GitHub.
2. Copie a URL do repositorio.
3. Rode:

```powershell
git branch -M main
git remote add origin <URL_DO_REPOSITORIO>
git push -u origin main
```

## Uso como package

Depois de publicar, o package pode ser instalado no Unity Package Manager por Git URL:

```text
https://github.com/Arantes83/terrainforger.git
```
