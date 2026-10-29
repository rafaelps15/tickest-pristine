"""Testes das instruções do Tickest para o agente.

Conferem que as skills de código do Tickest são adaptação declarada da biblioteca canônica
(`C:\\dev\\dotnet-universal-skills`) e que o `CLAUDE.md`, o agente `dotnet-architect` e o índice das skills
seguem o padrão do domínio rico.

Rodar na raiz do repositório:
    python -m unittest discover -s scripts -p "test_*.py"

A biblioteca é lida de DOTNET_SKILLS_LIBRARY (padrão C:\\dev\\dotnet-universal-skills). Sem ela, como no CI,
a comparação com a biblioteca é pulada e o resto roda.

Paridade: os blocos de código de cada par de arquivos são normalizados linha a linha (sem o prefixo
`TickestPristine.`, espaços colapsados, texto de comentário trocado por `…` mantendo as tags de `///`,
conteúdo de literais trocado por `…`) e comparados por `difflib.unified_diff`. O resultado precisa ser igual
ao diff esperado do par, guardado em `scripts/instruction_parity/` e aprovado pelo responsável; sem arquivo,
o diff esperado é vazio. Não se conferem valores de literais nem a tradução da prosa.
"""
from __future__ import annotations

import difflib
import os
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TICKEST_SKILLS = ROOT / ".claude" / "skills"
LIBRARY = Path(os.environ.get("DOTNET_SKILLS_LIBRARY", r"C:\dev\dotnet-universal-skills"))
LIBRARY_SKILLS = LIBRARY / ".claude" / "skills"
EXPECTED_DIR = Path(__file__).resolve().parent / "instruction_parity"

# Mapa de pares (mesmo caminho relativo nas duas árvores de skills).
PAIRS = (
    "add-entity/SKILL.md",
    "add-entity/references/rich-entity.md",
    "add-feature/SKILL.md",
    "add-feature/references/command-slice.md",
    "add-feature/references/query-slice.md",
    "add-feature/references/endpoint.md",
    "add-feature/references/tests.md",
    "add-feature/references/rich-domain-handler.md",
    "add-feature/references/authorization.md",
    "add-feature/references/abstraction.md",
    "add-feature/references/domain-event-handler.md",
    "add-tests/SKILL.md",
    "ca-review/SKILL.md",
    "domain-modeling/SKILL.md",
    "domain-modeling/references/patterns.md",
)
# Sem par, declarado: o Tickest não adota o conversor UTC nem a auditoria no SaveChanges.
LIBRARY_ONLY = ("add-entity/references/persistence.md",)
# Só no Tickest, fora da comparação.
TICKEST_ONLY = ("add-feature/references/pagination.md",)
TICKEST_ONLY_SKILLS = ("spec-new", "spec-implement", "spec-verify")

CODE_BLOCK_RE = re.compile(r"```[^\n]*\n(.*?)```", re.S)
STRING_RE = re.compile(r'"(?:\\.|[^"\\])*"')
DOC_TAG_RE = re.compile(r"</?(?:summary|param|typeparam|returns|remarks)\b[^>]*>")
BLOCK_MARKER = "```"

FORBIDDEN_MENTIONS = re.compile(r"milan|jovanovi|event-?reminder|kamil|grzybek|modular-monolith", re.I)
FORBIDDEN_PHRASES = (
    re.compile(r"(?i)no XML comments|nenhum comentário XML|sem comentários XML|sem XML summary"),
    re.compile(r"(?i)\bUseSeeding\b|\bHasData\b|\bseed"),
)
SPEC_IDS = re.compile(r"\b(SPEC|REQ|DEC)-\d")
# Caminhos que o agente dotnet-architect cria na primeira execução.
CREATED_BY_AGENT = {"docs/audit/architecture-audit.md"}


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def code_blocks(text: str) -> list[str]:
    return CODE_BLOCK_RE.findall(text.replace("\r\n", "\n"))


def split_comment(line: str) -> tuple[str, str]:
    """Separa o código do comentário de fim de linha, ignorando `//` dentro de literais."""
    in_string = escaped = False
    for i, char in enumerate(line):
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
        elif char == '"':
            in_string = True
        elif line.startswith("//", i):
            return line[:i], line[i:]
    return line, ""


def normalize_line(line: str) -> str:
    """Normaliza uma linha de código para a comparação de paridade."""
    code, comment = split_comment(line.replace("TickestPristine.", ""))
    code = STRING_RE.sub('"…"', code)
    if comment.startswith("///"):
        tags = DOC_TAG_RE.findall(comment)
        comment = "/// " + "…".join(tags) if tags else "///…"
    elif comment:
        comment = "//…"
    return re.sub(r"\s+", " ", f"{code} {comment}").strip()


def normalized(text: str) -> list[str]:
    """Linhas normalizadas de todos os blocos de código, na ordem, com um marcador por bloco."""
    lines: list[str] = []
    for block in code_blocks(text):
        lines.append(BLOCK_MARKER)
        lines += [n for n in (normalize_line(line) for line in block.splitlines()) if n]
    return lines


def parity_diff(library_text: str, tickest_text: str, name: str) -> list[str]:
    """Diff das linhas normalizadas; os cabeçalhos `@@` ficam sem números, para que uma linha nova antes de
    uma adaptação não mude o diff esperado."""
    diff = difflib.unified_diff(normalized(library_text), normalized(tickest_text),
                                fromfile=f"biblioteca/{name}", tofile=f"tickest/{name}", lineterm="", n=0)
    return ["@@" if line.startswith("@@") else line for line in diff]


def expected_file(name: str) -> Path:
    return EXPECTED_DIR / (name.replace("/", "__").removesuffix(".md") + ".diff")


def expected_diff(name: str) -> list[str]:
    path = expected_file(name)
    return read(path).splitlines() if path.exists() else []


def pair_problems(library_text: str, tickest_text: str, name: str, expected: list[str]) -> list[str]:
    """Diferenças entre o diff atual e o esperado: divergência nova ou diff esperado que deixou de ocorrer."""
    actual = parity_diff(library_text, tickest_text, name)
    if actual == expected:
        return []
    return [line for line in difflib.unified_diff(expected, actual, "esperado", "atual", lineterm="", n=0)]


def skill_files(skills_dir: Path, skip_skills: tuple[str, ...] = ()) -> set[str]:
    return {
        path.relative_to(skills_dir).as_posix()
        for path in skills_dir.rglob("*.md")
        if path.parent != skills_dir and path.relative_to(skills_dir).parts[0] not in skip_skills
    }


def section(text: str, heading: str) -> str:
    """Texto de uma seção (`## ` ou `### `) até a próxima seção de nível igual ou maior."""
    match = re.search(rf"^(#+) {re.escape(heading)}[^\n]*$", text, re.M)
    if not match:
        return ""
    level = len(match.group(1))
    end = re.search(rf"^#{{1,{level}}} ", text[match.end():], re.M)
    return text[match.start(): match.end() + end.start() if end else len(text)]


class ParityMethod(unittest.TestCase):
    """Autotestes do método de paridade, com pares sintéticos."""

    LIBRARY_TEXT = (
        "Prose in English.\n\n```csharp\n/// <summary>Project of an owner.</summary>\npublic sealed class Project : Entity\n"
        "{\n    // Required by EF Core.\n    private Project() { }\n\n    /// <summary>Name.</summary>\n"
        "    public string Name { get; private set; }\n\n    /// <summary>Archives.</summary>\n    public Result Archive()\n"
        "    {\n        Raise(new ProjectArchivedDomainEvent(Id));\n        return Result.Failure(Error.Problem(\"Projects.X\", \"Already archived\"));\n"
        "    }\n}\n```\n"
    )

    def tickest_text(self) -> str:
        return (self.LIBRARY_TEXT.replace("Prose in English.", "Prosa em português.")
                .replace("Project of an owner.", "Projeto de um dono.")
                .replace("// Required by EF Core.", "// Exigido pelo EF Core.")
                .replace("Already archived", "Projeto já arquivado")
                .replace("public sealed class Project", "public sealed class TickestPristine.Project"))

    def test_accepts_translation_of_comments_and_messages(self):
        self.assertEqual([], pair_problems(self.LIBRARY_TEXT, self.tickest_text(), "x.md", []))

    def test_rejects_private_set_turned_into_set(self):
        tickest = self.tickest_text().replace("{ get; private set; }", "{ get; set; }")
        self.assertNotEqual([], pair_problems(self.LIBRARY_TEXT, tickest, "x.md", []))

    def test_rejects_removed_summary(self):
        tickest = self.tickest_text().replace("    /// <summary>Name.</summary>\n", "")
        self.assertNotEqual([], pair_problems(self.LIBRARY_TEXT, tickest, "x.md", []))

    def test_rejects_removed_raise(self):
        tickest = self.tickest_text().replace("        Raise(new ProjectArchivedDomainEvent(Id));\n", "")
        self.assertNotEqual([], pair_problems(self.LIBRARY_TEXT, tickest, "x.md", []))

    def test_rejects_extra_block(self):
        tickest = self.tickest_text() + "\n```csharp\nint x = 1;\n```\n"
        self.assertNotEqual([], pair_problems(self.LIBRARY_TEXT, tickest, "x.md", []))

    def test_accepts_expected_diff_and_rejects_it_when_it_stops_occurring(self):
        tickest = self.tickest_text().replace("public Result Archive()", "public Result Archive(Guid requestedBy)")
        expected = parity_diff(self.LIBRARY_TEXT, tickest, "x.md")
        self.assertNotEqual([], expected)
        self.assertEqual([], pair_problems(self.LIBRARY_TEXT, tickest, "x.md", expected))
        self.assertNotEqual([], pair_problems(self.LIBRARY_TEXT, self.tickest_text(), "x.md", expected))


class SkillParity(unittest.TestCase):
    """As skills de código do Tickest só divergem da biblioteca pelo diff esperado de cada par."""

    def test_every_tickest_skill_file_is_mapped(self):
        unmapped = skill_files(TICKEST_SKILLS, TICKEST_ONLY_SKILLS) - set(PAIRS) - set(TICKEST_ONLY)
        self.assertEqual(set(), unmapped)
        self.assertEqual(set(), set(PAIRS) - skill_files(TICKEST_SKILLS))

    @unittest.skipUnless(LIBRARY_SKILLS.is_dir(), "biblioteca canônica não disponível nesta máquina")
    def test_every_library_skill_file_is_mapped(self):
        self.assertEqual(set(), skill_files(LIBRARY_SKILLS) - set(PAIRS) - set(LIBRARY_ONLY))

    @unittest.skipUnless(LIBRARY_SKILLS.is_dir(), "biblioteca canônica não disponível nesta máquina")
    def test_pairs_match_expected_diffs(self):
        for name in PAIRS:
            with self.subTest(par=name):
                problems = pair_problems(read(LIBRARY_SKILLS / name), read(TICKEST_SKILLS / name), name,
                                         expected_diff(name))
                self.assertEqual([], problems, "\n".join(problems))


class TickestInstructions(unittest.TestCase):
    """CLAUDE.md, agente dotnet-architect, índice das skills e frases proibidas."""

    def documents(self) -> list[Path]:
        documents = [TICKEST_SKILLS / name for name in PAIRS]
        if LIBRARY_SKILLS.is_dir():
            documents += [*LIBRARY_SKILLS.rglob("*.md"), LIBRARY / "CLAUDE.md", LIBRARY / "README.md",
                          *(LIBRARY / "docs").glob("*.md")]
        return documents

    def test_skills_have_no_forbidden_phrases_or_spec_ids_in_examples(self):
        for path in self.documents():
            text = read(path)
            for pattern in FORBIDDEN_PHRASES:
                self.assertIsNone(pattern.search(text), f"{path}: {pattern.pattern}")
            self.assertIsNone(SPEC_IDS.search("\n".join(code_blocks(text))), path)

    def test_claude_md_follows_rich_domain(self):
        text = read(ROOT / "CLAUDE.md")
        migration = section(text, "Em migração")
        for heading in ("Regras permanentes", "Eventos de domínio", "Desvios", "Em migração"):
            self.assertNotEqual("", section(text, heading), heading)
        outside_migration = text.replace(migration, "")
        self.assertNotRegex(outside_migration, r"(?i)domínio anêmico")
        self.assertRegex(migration, r"(?i)seed")
        self.assertIsNone(FORBIDDEN_MENTIONS.search(text))

    def test_dotnet_architect_agent(self):
        text = read(ROOT / ".claude" / "agents" / "dotnet-architect.md")
        frontmatter = re.match(r"^---\n(.*?)\n---\n", text.replace("\r\n", "\n"), re.S).group(1)
        for phrase in ("a solução inteira", "mesmo fora do escopo", "Sem XML summary"):
            self.assertNotIn(phrase, text)
        self.assertIsNone(FORBIDDEN_MENTIONS.search(text))
        tools = re.search(r"^tools:\s*(.+)$", frontmatter, re.M).group(1)
        self.assertIn("WebFetch", tools)
        self.assertIn("WebSearch", tools)
        self.assertRegex(text, r"(?is)referência técnica.*(CLAUDE\.md|spec|skills)|(CLAUDE\.md|spec|skills).*referência técnica")
        for cited in re.findall(r"`((?:\.claude|docs|specs|src|tests|scripts)/[^`*{}<>\s]+)`", text):
            if cited in CREATED_BY_AGENT:
                continue
            self.assertTrue((ROOT / cited).exists(), cited)

    def test_skills_readme_rows_point_to_existing_skills(self):
        rows = re.findall(r"^\|\s*\*\*([a-z0-9-]+)\*\*\s*\|", read(TICKEST_SKILLS / "README.md"), re.M)
        self.assertNotEqual([], rows)
        for name in rows:
            self.assertTrue((TICKEST_SKILLS / name / "SKILL.md").exists(), f".claude/skills/{name}/SKILL.md")
        self.assertNotIn("add-integration-event", read(TICKEST_SKILLS / "README.md"))


if __name__ == "__main__":
    unittest.main()
