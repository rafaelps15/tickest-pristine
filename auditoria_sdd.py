#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
==============================================================================
 AUDITORIA DE CONFORMIDADE OPERACIONAL — IA AGÊNTICA + SPEC-DRIVEN DEVELOPMENT
==============================================================================
Audita um repositório e verifica se ele está pronto para ser desenvolvido
por agentes de IA (Claude Code, Copilot, Cursor etc.) seguindo SDD:
spec -> plano -> tarefas -> código -> testes -> revisão humana.

Uso:
    python auditoria_sdd.py [caminho] [--formato console|md|json]
                            [--saida arquivo] [--min-score 70] [--commits 30]

Código de saída:
    0 = score >= min-score e nenhuma falha CRÍTICA
    1 = reprovado (útil para travar pipeline de CI)

Sem dependências externas (Python 3.8+).
"""
import argparse
import json
import re
import subprocess
import sys
from dataclasses import dataclass, field, asdict
from datetime import datetime
from pathlib import Path
from typing import List, Optional

# ---------------------------------------------------------------------------
# Configuração
# ---------------------------------------------------------------------------
PESOS = {"CRITICA": 3, "ALTA": 2, "MEDIA": 1}

PASTAS_IGNORADAS = {".git", "bin", "obj", "node_modules", ".vs", ".idea",
                    "packages", "dist", "build", ".venv", "venv", "TestResults"}

EXTENSOES_TEXTO = {".cs", ".json", ".yml", ".yaml", ".xml", ".config", ".env",
                   ".md", ".txt", ".ps1", ".sh", ".py", ".js", ".ts", ".props",
                   ".csproj", ".tf", ".ini", ".toml", ""}

ARQUIVOS_INSTRUCAO_AGENTE = ["CLAUDE.md", "AGENTS.md", ".github/copilot-instructions.md",
                             ".cursorrules", ".cursor/rules", "GEMINI.md"]

PASTAS_SPECS = ["specs", ".specify/specs", ".kiro/specs", "docs/specs", "spec"]

ARQUIVOS_CONSTITUICAO = [".specify/memory/constitution.md", "memory/constitution.md",
                         "constitution.md", "docs/constitution.md", ".kiro/steering"]

PADROES_SEGREDO = [
    ("Chave AWS", re.compile(r"AKIA[0-9A-Z]{16}")),
    ("Chave privada", re.compile(r"-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----")),
    ("Chave Anthropic/OpenAI", re.compile(r"sk-(ant-)?[A-Za-z0-9_\-]{20,}")),
    ("Token GitHub", re.compile(r"gh[pousr]_[A-Za-z0-9]{30,}")),
    ("Chave Google", re.compile(r"AIza[0-9A-Za-z_\-]{35}")),
    ("Senha em connection string",
     re.compile(r"(?i)(password|pwd)\s*=\s*(?!\s*;|\s*\"|\$\{|\{\{|<)([^;\"\s]{4,})")),
]

# Valores que casam com o padrão de senha mas não são segredo: a senha padrão do PostgreSQL
# local (docker compose / appsettings.Development.json) e atribuições booleanas.
VALORES_SENHA_NAO_SECRETOS = {"postgres", "true", "false"}

# Pastas cujas senhas são dados fixos de teste, não credenciais.
PASTAS_SEM_VARREDURA_SEGREDO = ("tests/",)

# Cabeçalho que faz o arquivo de instruções do agente valer como constituição.
RE_SECAO_CONSTITUICAO = re.compile(r"(?im)^#+\s*(regras permanentes|princ[ií]pios|constitui[cç][aã]o)")

# Seções de plano e de tarefas dentro de um spec.md único.
RE_SECAO_PLANO = re.compile(r"(?im)^#+\s*(\d+\.\s*)?(plano|plan)\b")
RE_SECAO_TAREFAS = re.compile(r"(?im)^#+\s*(\d+\.\s*)?(tarefas|tasks)\b")

RE_ID_REQUISITO = re.compile(r"\b(REQ|FR|NFR|RF|RNF|US|AC)-?\d{1,4}\b", re.IGNORECASE)
RE_EARS = re.compile(r"(?i)\b(when|quando|if|se)\b.{0,120}\b(the system shall|o sistema deve|shall|deve)\b")
RE_CRITERIO_ACEITE = re.compile(r"(?i)(crit[eé]rios? de aceite|acceptance criteria|given .* when .* then|dado .* quando .* ent[aã]o)")
RE_CHECKBOX = re.compile(r"^\s*[-*]\s*\[[ xX]\]", re.MULTILINE)
RE_COMMIT_RASTREAVEL = re.compile(r"(\b(REQ|FR|RF|US|T|TASK|SPEC)-?\d+\b|#\d+|^(feat|fix|refactor|test|docs|chore|perf|ci|build)(\(.+\))?!?:)", re.IGNORECASE)


# ---------------------------------------------------------------------------
# Modelo
# ---------------------------------------------------------------------------
@dataclass
class Resultado:
    dominio: str
    codigo: str
    descricao: str
    severidade: str
    status: str = "FALHA"          # OK | FALHA | N/A
    evidencia: str = ""
    recomendacao: str = ""


@dataclass
class Relatorio:
    repositorio: str
    data: str
    resultados: List[Resultado] = field(default_factory=list)

    @property
    def avaliados(self):
        return [r for r in self.resultados if r.status != "N/A"]

    @property
    def score(self) -> float:
        total = sum(PESOS[r.severidade] for r in self.avaliados)
        ok = sum(PESOS[r.severidade] for r in self.avaliados if r.status == "OK")
        return round(100 * ok / total, 1) if total else 0.0

    @property
    def falhas_criticas(self):
        return [r for r in self.resultados if r.status == "FALHA" and r.severidade == "CRITICA"]

    def classificacao(self) -> str:
        s = self.score
        if self.falhas_criticas:
            return "NÃO CONFORME (falha crítica)"
        if s >= 85: return "CONFORME"
        if s >= 70: return "PARCIALMENTE CONFORME"
        return "NÃO CONFORME"


# ---------------------------------------------------------------------------
# Utilitários
# ---------------------------------------------------------------------------
class Repo:
    def __init__(self, raiz: Path):
        self.raiz = raiz
        self._arquivos: Optional[List[Path]] = None

    def arquivos(self) -> List[Path]:
        if self._arquivos is None:
            lista = []
            for p in self.raiz.rglob("*"):
                if any(parte in PASTAS_IGNORADAS for parte in p.relative_to(self.raiz).parts):
                    continue
                if p.is_file():
                    lista.append(p)
            self._arquivos = lista
        return self._arquivos

    def existe(self, rel: str) -> bool:
        return (self.raiz / rel).exists()

    def ler(self, p: Path, limite: int = 500_000) -> str:
        try:
            if p.stat().st_size > limite:
                return ""
            return p.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            return ""

    def rel(self, p: Path) -> str:
        return str(p.relative_to(self.raiz)).replace("\\", "/")

    def buscar(self, padrao_glob: str) -> List[Path]:
        return [p for p in self.arquivos() if p.match(padrao_glob)]

    def git(self, *args) -> Optional[str]:
        try:
            out = subprocess.run(["git", "-C", str(self.raiz), *args],
                                 capture_output=True, text=True, timeout=20)
            return out.stdout if out.returncode == 0 else None
        except (OSError, subprocess.TimeoutExpired):
            return None


def senha_suspeita(m: re.Match, p: Path) -> bool:
    """Descarta valores conhecidos que não são segredo e, em C#, atribuições de código
    (`password = valor`); ali só conta o formato de connection string (`Password=valor`)."""
    if p.suffix.lower() == ".cs" and re.search(r"\s=|=\s", m.group(0)):
        return False
    return m.group(2).strip("`'),.").lower() not in VALORES_SENHA_NAO_SECRETOS


# ---------------------------------------------------------------------------
# Verificações
# ---------------------------------------------------------------------------
def auditar(repo: Repo, n_commits: int) -> List[Resultado]:
    R: List[Resultado] = []

    def add(dom, cod, desc, sev, ok, evid="", rec="", na=False):
        R.append(Resultado(dom, cod, desc, sev,
                           "N/A" if na else ("OK" if ok else "FALHA"),
                           evid, "" if ok else rec))

    # ---------------- 1. GOVERNANÇA DO AGENTE ----------------
    D = "1. Governança do Agente"
    instr = [a for a in ARQUIVOS_INSTRUCAO_AGENTE if repo.existe(a)]
    add(D, "GOV-01", "Arquivo de instruções do agente (CLAUDE.md / AGENTS.md)", "CRITICA",
        bool(instr), ", ".join(instr) or "nenhum encontrado",
        "Crie um CLAUDE.md/AGENTS.md na raiz com contexto, arquitetura, comandos e regras.")

    conteudo_instr = ""
    for a in instr:
        p = repo.raiz / a
        if p.is_file():
            conteudo_instr += repo.ler(p) + "\n"

    add(D, "GOV-02", "Instruções têm conteúdo substancial (>= 500 caracteres)", "ALTA",
        len(conteudo_instr.strip()) >= 500, f"{len(conteudo_instr.strip())} caracteres",
        "Detalhe arquitetura, convenções, comandos e o que o agente NÃO pode fazer.", na=not instr)

    tem_cmds = bool(re.search(r"(dotnet (build|test)|npm (run|test)|make |pytest|mvn )", conteudo_instr))
    add(D, "GOV-03", "Instruções informam comandos de build/teste", "ALTA", tem_cmds,
        "comandos encontrados" if tem_cmds else "nenhum comando de build/teste citado",
        "Inclua os comandos exatos (ex.: `dotnet build`, `dotnet test`) para o agente validar o próprio trabalho.",
        na=not instr)

    tem_spec_ref = bool(re.search(r"(?i)(spec|especifica|requisit|tasks\.md|plan\.md)", conteudo_instr))
    add(D, "GOV-04", "Instruções obrigam o agente a seguir as specs", "ALTA", tem_spec_ref,
        "referência a specs encontrada" if tem_spec_ref else "sem menção a specs",
        "Adicione regra: 'Nenhum código sem spec aprovada; atualize tasks.md ao concluir cada tarefa'.",
        na=not instr)

    settings = [p for p in (repo.raiz / ".claude/settings.json", repo.raiz / ".claude/settings.local.json") if p.exists()]
    perm_txt = "".join(repo.ler(p) for p in settings)
    try:
        perm = json.loads(repo.ler(settings[0])) if settings else {}
    except json.JSONDecodeError:
        perm = {}
    deny = perm.get("permissions", {}).get("deny", [])
    add(D, "GOV-05", "Permissões do agente com regras de bloqueio (deny)", "CRITICA", bool(deny),
        f"{len(deny)} regra(s) deny" if settings else "sem .claude/settings.json",
        "Configure `.claude/settings.json` com `permissions.deny` (ex.: Read(./.env), Bash(git push:*), Bash(rm -rf:*)).")

    protege_env = bool(re.search(r"\.env|secrets|appsettings\.Production", perm_txt))
    add(D, "GOV-06", "Agente bloqueado de ler segredos (.env / secrets)", "ALTA", protege_env,
        "regra encontrada" if protege_env else "nenhuma regra para .env/secrets",
        "Adicione `Read(./.env)`, `Read(./**/secrets/**)` ao deny.", na=not settings)

    tem_hooks = "hooks" in perm
    add(D, "GOV-07", "Hooks de verificação automática configurados", "MEDIA", tem_hooks,
        "hooks presentes" if tem_hooks else "sem hooks",
        "Use hooks (ex.: PostToolUse rodando `dotnet format`/`dotnet build`) para validar cada edição do agente.",
        na=not settings)

    # ---------------- 2. ESPECIFICAÇÕES (SDD) ----------------
    D = "2. Especificações (SDD)"
    pastas = [p for p in PASTAS_SPECS if (repo.raiz / p).is_dir()]
    add(D, "SDD-01", "Diretório de especificações existe", "CRITICA", bool(pastas),
        ", ".join(pastas) or "nenhum (specs/, .specify/, .kiro/specs, docs/specs)",
        "Crie `specs/<feature>/` com spec.md, plan.md e tasks.md para cada funcionalidade.")

    constit = [c for c in ARQUIVOS_CONSTITUICAO if repo.existe(c)]
    if not constit:
        constit = [a for a in instr if (repo.raiz / a).is_file()
                   and RE_SECAO_CONSTITUICAO.search(repo.ler(repo.raiz / a))]
    add(D, "SDD-02", "Constituição / princípios do projeto definidos", "ALTA", bool(constit),
        ", ".join(constit) or "não encontrada",
        "Crie `memory/constitution.md` com princípios inegociáveis (arquitetura, testes, segurança).")

    md_specs: List[Path] = []
    for p in pastas:
        md_specs += [f for f in (repo.raiz / p).rglob("*.md")]
    # A raiz da pasta de specs (backlog, índices) e subpastas iniciadas por "_" (fontes,
    # rascunhos) não são features.
    raizes_specs = {repo.raiz / p for p in pastas}
    features = sorted({f.parent for f in md_specs
                       if f.parent not in raizes_specs and not f.parent.name.startswith("_")})

    def nomes(d: Path):
        return {f.name.lower() for f in d.glob("*.md")}

    completas, incompletas = [], []
    for d in features:
        n = nomes(d)
        tem_req = bool(n & {"spec.md", "requirements.md", "requisitos.md"})
        txt_spec = repo.ler(d / "spec.md") if "spec.md" in n else ""
        tem_design = bool(n & {"plan.md", "design.md", "plano.md"}) or bool(RE_SECAO_PLANO.search(txt_spec))
        tem_tasks = bool(n & {"tasks.md", "tarefas.md"}) or bool(RE_SECAO_TAREFAS.search(txt_spec))
        (completas if (tem_req and tem_design and tem_tasks) else incompletas).append(repo.rel(d))

    add(D, "SDD-03", "Cada feature tem spec + plano + tarefas", "CRITICA",
        bool(features) and not incompletas,
        f"{len(completas)} completa(s), {len(incompletas)} incompleta(s)"
        + (f": {', '.join(incompletas[:5])}" if incompletas else ""),
        "Toda feature precisa dos 3 artefatos: spec.md (o quê), plan.md (como), tasks.md (passos).",
        na=not pastas)

    req_files = [f for f in md_specs if f.name.lower() in {"spec.md", "requirements.md", "requisitos.md"}]
    txt_req = "\n".join(repo.ler(f) for f in req_files)
    ids = set(m.group(0).upper() for m in RE_ID_REQUISITO.finditer(txt_req))
    add(D, "SDD-04", "Requisitos com identificadores únicos (FR-001, REQ-01...)", "ALTA", bool(ids),
        f"{len(ids)} ID(s) encontrado(s)", "Numere cada requisito (ex.: FR-001) para permitir rastreabilidade.",
        na=not req_files)

    add(D, "SDD-05", "Critérios de aceite testáveis (Dado/Quando/Então ou EARS)", "ALTA",
        bool(RE_CRITERIO_ACEITE.search(txt_req) or RE_EARS.search(txt_req)),
        "encontrados" if (RE_CRITERIO_ACEITE.search(txt_req) or RE_EARS.search(txt_req)) else "ausentes",
        "Escreva critérios verificáveis: 'Dado X, Quando Y, Então Z'.", na=not req_files)

    ambig = len(re.findall(r"\[(NEEDS CLARIFICATION|A DEFINIR|TBD|TODO)[^\]]*\]", txt_req, re.IGNORECASE))
    add(D, "SDD-06", "Specs sem pendências de esclarecimento", "MEDIA", ambig == 0,
        f"{ambig} marcador(es) NEEDS CLARIFICATION/TBD", "Resolva as ambiguidades antes de liberar para o agente.",
        na=not req_files)

    task_files = [f for f in md_specs if f.name.lower() in {"tasks.md", "tarefas.md"}]
    txt_tasks = "\n".join(repo.ler(f) for f in task_files)
    caixas = RE_CHECKBOX.findall(txt_tasks)
    add(D, "SDD-07", "Tarefas em checklist rastreável ([ ] / [x])", "MEDIA", bool(caixas),
        f"{len(caixas)} tarefa(s) em checklist", "Use `- [ ] T001 descrição` para o agente marcar progresso.",
        na=not task_files)

    # ---------------- 3. RASTREABILIDADE ----------------
    D = "3. Rastreabilidade"
    ids_tasks = set(m.group(0).upper() for m in RE_ID_REQUISITO.finditer(txt_tasks))
    cobertos = ids & ids_tasks
    pct = round(100 * len(cobertos) / len(ids)) if ids else 0
    add(D, "RAS-01", "Requisitos referenciados nas tarefas (>= 80%)", "ALTA", pct >= 80,
        f"{len(cobertos)}/{len(ids)} requisitos cobertos ({pct}%)",
        "Cada tarefa deve citar o requisito que implementa (ex.: 'T003 [FR-002] ...').",
        na=not ids or not task_files)

    # Commits de merge (inclusive o merge sintético que o CI cria num PR) não são escritos à mão.
    log = repo.git("log", "--no-merges", f"-{n_commits}", "--pretty=%s")
    if log is None:
        add(D, "RAS-02", "Commits rastreáveis (Conventional Commits / IDs)", "MEDIA", False,
            "repositório git indisponível", "", na=True)
    else:
        msgs = [m for m in log.splitlines() if m.strip()]
        rastreaveis = [m for m in msgs if RE_COMMIT_RASTREAVEL.search(m)]
        pc = round(100 * len(rastreaveis) / len(msgs)) if msgs else 0
        add(D, "RAS-02", "Commits rastreáveis (Conventional Commits / IDs) >= 70%", "MEDIA", pc >= 70,
            f"{len(rastreaveis)}/{len(msgs)} commits ({pc}%)",
            "Padronize: `feat(tickets): criar endpoint [FR-003]`.", na=not msgs)

    co_autor = (repo.git("log", "--no-merges", f"-{n_commits}", "--pretty=%b") or "")
    add(D, "RAS-03", "Commits do agente identificados (Co-Authored-By)", "MEDIA",
        "co-authored-by" in co_autor.lower(),
        "trailer encontrado" if "co-authored-by" in co_autor.lower() else "nenhum commit identificado como gerado por IA",
        "Mantenha o trailer `Co-Authored-By` em commits do agente para auditoria.", na=log is None)

    # ---------------- 4. QUALIDADE (.NET) ----------------
    D = "4. Qualidade de Código (.NET)"
    csprojs = repo.buscar("*.csproj")
    eh_dotnet = bool(csprojs or repo.buscar("*.sln") or repo.buscar("*.slnx"))
    testes = [p for p in csprojs if re.search(r"(?i)test", p.name)]
    add(D, "QUA-01", "Projetos de teste existem", "CRITICA", bool(testes),
        f"{len(testes)} projeto(s) de teste: " + ", ".join(p.name for p in testes[:4]) if testes else "nenhum *Tests.csproj",
        "Crie projetos de teste (xUnit) — o agente precisa de testes para validar o que gera.", na=not eh_dotnet)

    dbp = repo.raiz / "Directory.Build.props"
    txt_props = repo.ler(dbp) if dbp.exists() else "\n".join(repo.ler(p) for p in csprojs)
    add(D, "QUA-02", "Nullable habilitado", "MEDIA",
        bool(re.search(r"<Nullable>\s*enable", txt_props, re.I)),
        "Directory.Build.props" if dbp.exists() else "verificado nos .csproj",
        "Adicione `<Nullable>enable</Nullable>` no Directory.Build.props.", na=not eh_dotnet)

    add(D, "QUA-03", "Warnings tratados como erro", "MEDIA",
        bool(re.search(r"<TreatWarningsAsErrors>\s*true", txt_props, re.I)),
        "", "Adicione `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` para barrar código frouxo do agente.",
        na=not eh_dotnet)

    add(D, "QUA-04", "Padrão de estilo (.editorconfig)", "MEDIA", repo.existe(".editorconfig"),
        "", "Adicione `.editorconfig` para o agente seguir o estilo do time.")

    add(D, "QUA-05", "Versão do SDK fixada (global.json)", "MEDIA", repo.existe("global.json"),
        "", "Fixe o SDK com `dotnet new globaljson` para builds reproduzíveis.", na=not eh_dotnet)

    # ---------------- 5. CI/CD ----------------
    D = "5. CI/CD"
    ci = (repo.buscar(".github/workflows/*.yml") + repo.buscar(".github/workflows/*.yaml")
          + [repo.raiz / f for f in (".gitlab-ci.yml", "cloudbuild.yaml", "cloudbuild.yml",
                                     "azure-pipelines.yml", "Jenkinsfile") if repo.existe(f)])
    add(D, "CI-01", "Pipeline de CI configurado", "CRITICA", bool(ci),
        ", ".join(repo.rel(p) for p in ci[:4]) or "nenhum pipeline",
        "Crie um pipeline (GitHub Actions / Cloud Build) que rode build e testes em todo PR.")

    txt_ci = "\n".join(repo.ler(p) for p in ci)
    add(D, "CI-02", "CI executa testes automatizados", "ALTA",
        bool(re.search(r"(dotnet test|npm test|pytest|mvn test)", txt_ci)),
        "", "Inclua `dotnet test` no pipeline.", na=not ci)

    add(D, "CI-03", "CI roda este script de auditoria (gate de conformidade)", "MEDIA",
        "auditoria_sdd" in txt_ci, "", "Adicione `python auditoria_sdd.py . --min-score 70` como etapa do CI.",
        na=not ci)

    # ---------------- 6. SEGURANÇA ----------------
    D = "6. Segurança"
    gi = repo.ler(repo.raiz / ".gitignore") if repo.existe(".gitignore") else ""
    add(D, "SEG-01", ".gitignore protege .env e segredos", "ALTA", ".env" in gi,
        ".env listado" if ".env" in gi else ("sem .gitignore" if not gi else ".env não listado"),
        "Adicione `.env`, `*.pfx`, `appsettings.*.local.json` ao .gitignore.")

    rastreados = repo.git("ls-files")
    lista_env = ([l for l in rastreados.splitlines() if re.search(r"(^|/)\.env(\.|$)", l) and not l.endswith(".example")]
                 if rastreados else [repo.rel(p) for p in repo.buscar(".env")])
    add(D, "SEG-02", "Nenhum arquivo .env versionado", "CRITICA", not lista_env,
        ", ".join(lista_env[:5]) or "nenhum", "Remova do histórico (git rm --cached) e rotacione as credenciais.")

    achados = []
    for p in repo.arquivos():
        if p.suffix.lower() not in EXTENSOES_TEXTO:
            continue
        # O próprio script contém os padrões e exemplos que procura.
        if repo.rel(p).startswith(PASTAS_SEM_VARREDURA_SEGREDO) or p.resolve() == Path(__file__).resolve():
            continue
        txt = repo.ler(p, limite=1_000_000)
        for nome, rx in PADROES_SEGREDO:
            eh_senha = nome == "Senha em connection string"
            if any(not eh_senha or senha_suspeita(m, p) for m in rx.finditer(txt)):
                achados.append(f"{repo.rel(p)} ({nome})")
                break
        if len(achados) >= 10:
            break
    add(D, "SEG-03", "Nenhum segredo exposto no código", "CRITICA", not achados,
        "; ".join(achados) or "nenhum padrão suspeito",
        "Mova segredos para User Secrets / Secret Manager (GCP) e rotacione as chaves expostas.")

    add(D, "SEG-04", "Configurações de MCP sem credenciais inline", "ALTA",
        not re.search(r'"(api[_-]?key|token|secret)"\s*:\s*"[^"$]{8,}"', repo.ler(repo.raiz / ".mcp.json"), re.I),
        ".mcp.json analisado" if repo.existe(".mcp.json") else "", "Use variáveis de ambiente (${VAR}) em .mcp.json.",
        na=not repo.existe(".mcp.json"))

    # ---------------- 7. REVISÃO HUMANA ----------------
    D = "7. Revisão Humana (Human-in-the-loop)"
    pr_tpl = [f for f in (".github/pull_request_template.md", ".github/PULL_REQUEST_TEMPLATE.md",
                          "docs/pull_request_template.md") if repo.existe(f)]
    add(D, "REV-01", "Template de PR exige vínculo com spec", "ALTA", bool(pr_tpl),
        ", ".join(pr_tpl) or "sem template",
        "Crie `.github/pull_request_template.md` com: spec vinculada, requisitos atendidos, testes, checklist.")

    owners = [f for f in ("CODEOWNERS", ".github/CODEOWNERS", "docs/CODEOWNERS") if repo.existe(f)]
    add(D, "REV-02", "CODEOWNERS define revisores obrigatórios", "MEDIA", bool(owners),
        ", ".join(owners) or "ausente", "Crie CODEOWNERS para que nenhum PR do agente entre sem dono revisando.")

    regra_push = bool(re.search(r"git push|git commit", perm_txt)) or \
                 bool(re.search(r"(?i)(n[aã]o (fa[cç]a|execute) (commit|push)|never (commit|push)|revis[aã]o humana)", conteudo_instr))
    add(D, "REV-03", "Agente impedido de commitar/push sem revisão", "ALTA", regra_push,
        "regra encontrada" if regra_push else "nenhuma restrição a commit/push",
        "Adicione `Bash(git push:*)` ao deny e a regra 'commits só após revisão do diff' no CLAUDE.md.")

    return R


# ---------------------------------------------------------------------------
# Saída
# ---------------------------------------------------------------------------
ICONE = {"OK": "✅", "FALHA": "❌", "N/A": "➖"}


def gerar_markdown(rel: Relatorio) -> str:
    L = [f"# Auditoria de Conformidade Operacional — IA Agêntica + SDD",
         "", f"**Repositório:** `{rel.repositorio}`  ",
         f"**Data:** {rel.data}  ",
         f"**Score:** **{rel.score}%** — **{rel.classificacao()}**  ",
         f"**Falhas críticas:** {len(rel.falhas_criticas)}", ""]
    dominio = None
    for r in rel.resultados:
        if r.dominio != dominio:
            dominio = r.dominio
            L += ["", f"## {dominio}", "", "| | Código | Verificação | Severidade | Evidência |",
                  "|---|---|---|---|---|"]
        L.append(f"| {ICONE[r.status]} | {r.codigo} | {r.descricao} | {r.severidade} | {r.evidencia.replace('|', '/')} |")
    falhas = [r for r in rel.resultados if r.status == "FALHA"]
    if falhas:
        L += ["", "## Plano de Ação (ordenado por severidade)", ""]
        for r in sorted(falhas, key=lambda x: -PESOS[x.severidade]):
            L.append(f"- **[{r.severidade}] {r.codigo}** — {r.recomendacao}")
    return "\n".join(L) + "\n"


def imprimir_console(rel: Relatorio):
    print("=" * 78)
    print(" AUDITORIA DE CONFORMIDADE OPERACIONAL — IA AGÊNTICA + SPEC-DRIVEN DEVELOPMENT")
    print("=" * 78)
    print(f" Repositório: {rel.repositorio}\n Data: {rel.data}\n")
    dominio = None
    for r in rel.resultados:
        if r.dominio != dominio:
            dominio = r.dominio
            print(f"\n■ {dominio}")
        print(f"  {ICONE[r.status]} [{r.severidade:<7}] {r.codigo} {r.descricao}")
        if r.evidencia:
            print(f"        └ {r.evidencia}")
    print("\n" + "-" * 78)
    print(f" SCORE: {rel.score}%   |   RESULTADO: {rel.classificacao()}")
    print(f" Falhas críticas: {len(rel.falhas_criticas)}")
    falhas = sorted([r for r in rel.resultados if r.status == "FALHA"], key=lambda x: -PESOS[x.severidade])
    if falhas:
        print("\n PLANO DE AÇÃO:")
        for r in falhas:
            print(f"  • [{r.severidade}] {r.codigo}: {r.recomendacao}")
    print("-" * 78)


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description="Auditoria de conformidade IA agêntica + SDD")
    ap.add_argument("caminho", nargs="?", default=".", help="raiz do repositório (padrão: .)")
    ap.add_argument("--formato", choices=["console", "md", "json"], default="console")
    ap.add_argument("--saida", help="grava o relatório neste arquivo")
    ap.add_argument("--min-score", type=float, default=70.0, help="score mínimo para aprovar (padrão 70)")
    ap.add_argument("--commits", type=int, default=30, help="quantos commits analisar (padrão 30)")
    a = ap.parse_args()

    raiz = Path(a.caminho).resolve()
    if not raiz.is_dir():
        sys.exit(f"Caminho inválido: {raiz}")

    rel = Relatorio(str(raiz), datetime.now().strftime("%Y-%m-%d %H:%M"))
    rel.resultados = auditar(Repo(raiz), a.commits)

    if a.formato == "console":
        imprimir_console(rel)
        texto = None
    elif a.formato == "md":
        texto = gerar_markdown(rel)
    else:
        texto = json.dumps({"repositorio": rel.repositorio, "data": rel.data, "score": rel.score,
                            "classificacao": rel.classificacao(),
                            "resultados": [asdict(r) for r in rel.resultados]},
                           ensure_ascii=False, indent=2)

    if texto is not None:
        if a.saida:
            Path(a.saida).write_text(texto, encoding="utf-8")
            print(f"Relatório gravado em {a.saida} — score {rel.score}% ({rel.classificacao()})")
        else:
            print(texto)
    elif a.saida:
        Path(a.saida).write_text(gerar_markdown(rel), encoding="utf-8")

    aprovado = rel.score >= a.min_score and not rel.falhas_criticas
    sys.exit(0 if aprovado else 1)


if __name__ == "__main__":
    main()
