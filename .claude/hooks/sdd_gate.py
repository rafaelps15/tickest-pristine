"""Portão SDD do repositório: hook do Claude Code.

Uso (configurado em .claude/settings.json):
  python sdd_gate.py prompt   -> evento UserPromptSubmit
  python sdd_gate.py tool     -> evento PreToolUse

Regra: escrever em src/ ou tests/ (por Edit, Write, NotebookEdit ou comando de
shell) e alterar o próprio portão só é permitido quando
  (a) uma mensagem do usuário iniciada por "/spec-implement NNN" abriu a
      implementação nesta sessão e a spec NNN está com "status: Em implementação"
      e "aprovada-em" preenchido (vale até outro /spec-implement ou até a spec
      sair de "Em implementação"); ou
  (b) a mensagem atual do usuário contém o marcador [sdd:autorizo].
Alterar o próprio portão exige sempre o marcador.

O estado de cada sessão fica fora do repositório (pasta temporária do sistema).
O portão impede erros do modelo; não é uma barreira contra quem queira burlá-lo
de propósito (ex.: montar caminhos em variáveis num comando de shell).
"""

from __future__ import annotations

import json
import os
import re
import sys
import tempfile
import time
from pathlib import Path

AUTHORIZATION_TOKEN = "[sdd:autorizo]"
STATE_DIR_NAME = "claude-sdd-gate"
STATUS_IN_IMPLEMENTATION = "Em implementação"
GATED_TOP_LEVEL_DIRS = ("src", "tests")
PROTECTED_RELATIVE_PATHS = (".claude/hooks", ".claude/settings.json", ".claude/settings.local.json")
# Defesa extra: se o UserPromptSubmit falhar, uma autorização antiga não vale para sempre.
AUTHORIZATION_TTL_SECONDS = 60 * 60

SPEC_IMPLEMENT_PATTERN = re.compile(r"^\s*/spec-implement\s+(\S+)", re.IGNORECASE)
SPEC_STATUS_PATTERN = re.compile(r"^status:\s*([^#\r\n]+)", re.MULTILINE)
SPEC_APPROVED_AT_PATTERN = re.compile(r"^aprovada-em:[ \t]*([^\s#]+)", re.MULTILINE)

# Um caminho que começa em src/ ou tests/ (relativo) ou que passa por \src\ ou /tests/ (absoluto).
GATED_PATH_PATTERN = re.compile(r"(?i)(^|[\s'\"=(:/\\<>|&;,])(src|tests)([/\\]|[\s'\";)|&<>,]|$)")
PROTECTED_PATH_PATTERN = re.compile(
    r"(?i)(\.claude[/\\]hooks|\.claude[/\\]settings(\.local)?\.json|" + re.escape(STATE_DIR_NAME) + ")"
)

# Comandos que escrevem no alvo citado no próprio comando (avaliados por trecho).
WRITE_COMMAND_PATTERNS = (
    re.compile(
        r"(?i)\b(set-content|add-content|out-file|new-item|remove-item|move-item|copy-item|rename-item|"
        r"clear-content|tee-object|expand-archive|writealltext|writeallbytes|writealllines|appendalltext|"
        r"invoke-webrequest|robocopy|xcopy)\b"
    ),
    re.compile(r"(?i)\[(system\.)?io\.(file|directory)\]::(write|append|copy|move|delete|create)"),
    re.compile(
        r"(?i)(^|[\s(`])(rm|mv|cp|touch|mkdir|rmdir|tee|ln|truncate|dd|install|del|erase|copy|move|ren|"
        r"ni|sc|ac|md|rd|cpi|mi|ri|rni|clc|patch|tar|unzip|rsync|mklink)(\s|$)"
    ),
    re.compile(r"(?i)\b(sed|perl)\b[^|;&]*\s(-\w*i\w*|--in-place)\b"),
    re.compile(r"(?i)\bdotnet\s+(ef|new|add|remove|sln)\b"),
    re.compile(r"(?i)\b(curl|wget)\b.*\s-(o|O|-output)\b"),
)
# Comandos que mudam a árvore de trabalho mesmo sem citar caminho.
TREE_CHANGING_PATTERN = re.compile(
    r"(?i)\bgit\s+(-C\s+\S+\s+)?(checkout|switch|restore|stash|apply|am|merge|pull|rebase|cherry-pick|revert|"
    r"clean|mv|rm|reset\s+[^;&|]*--hard)\b|\bdotnet\s+format\b"
)
GIT_COMMIT_ONLY_PATTERN = re.compile(r"(?is)\s*(cd\s+[^;&|]+(&&|;)\s*)?git\s+(-C\s+\S+\s+)?commit\b[^;&|]*")
HEREDOC_SUBSTITUTION_PATTERN = re.compile(r"(?s)\$\(\s*cat\s+<<-?\s*['\"]?(\w+)['\"]?\n.*?\n\1\s*\)")
CD_PATTERN = re.compile(r"(?i)^\s*(cd|set-location|sl|pushd|chdir)\s+(['\"]?)([^'\";&|]+)\2\s*$")
REDIRECT_TARGET_PATTERN = re.compile(r"(?<![0-9&<>=!-])>{1,2}(?!&)\s*(\"[^\"]+\"|'[^']+'|[^\s;|&)]+)")
TEE_TARGET_PATTERN = re.compile(r"(?i)(^|[\s|])(tee|tee-object)(\s+-a|\s+-append|\s+-filepath)?\s+(\"[^\"]+\"|'[^']+'|[^\s;|&)-][^\s;|&)]*)")
OUT_FILE_TARGET_PATTERN = re.compile(
    r"(?i)\b(out-file|set-content|add-content)\b(\s+-(file)?path)?\s+(\"[^\"]+\"|'[^']+'|[^\s;|&)-][^\s;|&)]*)"
)
NULL_TARGETS = {"$null", "/dev/null", "nul"}
# Alvo que só se conhece na execução (variável ou substituição de comando).
DYNAMIC_TARGET_PATTERN = re.compile(r"^[\"']?(\$|%|`)")
# Comando codificado: o conteúdo não pode ser inspecionado.
ENCODED_COMMAND_PATTERN = re.compile(r"(?i)(^|\s)-(e|ec|enc|encodedcommand)\s+[A-Za-z0-9+/=]{8,}")
INLINE_INTERPRETER_PATTERN = re.compile(
    r"(?i)(^|[\s;&|(`])(python[0-9.]*|py|node|ruby|perl|pwsh|powershell)(\.exe)?\s(.*\s)?(-c|-e|-command|-)(\s|$)|<<"
)
INLINE_CODE_WRITE_PATTERN = re.compile(
    r"(?i)(open\([^)]*['\"][wax+]|\.write(_text|_bytes|lines)?\(|unlink|rmtree|os\.remove|os\.rename|os\.replace|"
    r"shutil\.|writefile|appendfile|set-content|add-content|out-file|remove-item|move-item|copy-item|new-item|"
    r"writeall|>\s*[^\s&])"
)

DENY_REASON_GATED = (
    "Portão SDD: escrita em src/ ou tests/ bloqueada. Ela só é permitida durante "
    "/spec-implement de uma spec aprovada com status 'Em implementação', ou numa mensagem do "
    "usuário que contenha [sdd:autorizo] (mudança trivial autorizada). Num pedido direto, "
    "procure em specs/ uma spec que cubra o pedido e indique /spec-implement NNN; se não "
    "houver, use a skill spec-new. Não tente contornar este bloqueio: informe o usuário."
)
DENY_REASON_PROTECTED = (
    "Portão SDD: alterar o próprio portão (.claude/hooks, .claude/settings*.json ou o estado "
    "da sessão) exige autorização explícita: a mensagem do usuário precisa conter [sdd:autorizo]. "
    "Informe o usuário em vez de tentar outro caminho."
)


def state_dir() -> Path:
    override = os.environ.get("SDD_GATE_STATE_DIR")
    return Path(override) if override else Path(tempfile.gettempdir()) / STATE_DIR_NAME


def state_file(session_id: str) -> Path:
    safe_id = re.sub(r"[^A-Za-z0-9_-]", "_", session_id or "sem-sessao")
    return state_dir() / f"{safe_id}.json"


def load_state(session_id: str) -> dict:
    try:
        return json.loads(state_file(session_id).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def save_state(session_id: str, state: dict) -> None:
    path = state_file(session_id)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(state, ensure_ascii=False), encoding="utf-8")
    os.replace(temporary, path)


def project_root(payload: dict) -> Path:
    root = os.environ.get("CLAUDE_PROJECT_DIR") or payload.get("cwd") or os.getcwd()
    return Path(root).resolve()


def relative_to_root(path_text: str, root: Path, cwd: str | Path | None) -> str | None:
    """Caminho relativo à raiz com barras normais, ou None se estiver fora dela."""
    if not path_text:
        return None
    path_text = path_text.strip("'\"")
    posix_drive = re.match(r"^/([a-zA-Z])/(.*)$", path_text)  # /c/dev/... do Git Bash
    if posix_drive:
        path_text = f"{posix_drive.group(1)}:/{posix_drive.group(2)}"
    path = Path(path_text)
    if not path.is_absolute():
        path = Path(cwd or root) / path
    try:
        relative = os.path.relpath(path.resolve(), root)
    except ValueError:  # outra unidade no Windows
        return None
    relative = relative.replace("\\", "/")
    return None if relative.startswith("..") else relative


def is_gated_relative(relative: str) -> bool:
    return relative.split("/", 1)[0].lower() in GATED_TOP_LEVEL_DIRS


def is_protected_relative(relative: str) -> bool:
    lowered = relative.lower()
    return any(lowered == p or lowered.startswith(p + "/") for p in PROTECTED_RELATIVE_PATHS)


def classify_relative(relative: str | None) -> str | None:
    if relative is None:
        return None
    if is_protected_relative(relative):
        return "protected"
    return "gated" if is_gated_relative(relative) else None


def spec_file(root: Path, spec_ref: str) -> Path | None:
    candidate = root / spec_ref
    if candidate.is_dir():
        return candidate / "spec.md"
    specs_dir = root / "specs"
    if specs_dir.is_dir():
        for folder in sorted(specs_dir.iterdir()):
            if folder.is_dir() and folder.name.startswith(spec_ref):
                return folder / "spec.md"
    return None


def spec_allows_implementation(root: Path, spec_ref: str) -> bool:
    path = spec_file(root, spec_ref)
    try:
        text = path.read_text(encoding="utf-8") if path else ""
    except OSError:
        return False
    status = SPEC_STATUS_PATTERN.search(text)
    approved_at = SPEC_APPROVED_AT_PATTERN.search(text)
    return bool(status and status.group(1).strip() == STATUS_IN_IMPLEMENTATION and approved_at)


def turn_is_authorized(state: dict) -> bool:
    return bool(state.get("authorized_turn")) and time.time() - state.get("prompt_at", 0) <= AUTHORIZATION_TTL_SECONDS


def handle_prompt(payload: dict) -> None:
    session_id = payload.get("session_id", "")
    prompt = payload.get("prompt") or ""
    state = load_state(session_id)
    match = SPEC_IMPLEMENT_PATTERN.match(prompt)
    if match:
        state["spec_implement"] = match.group(1).strip().strip("'\"")
    # A autorização por marcador vale só para a mensagem em que aparece.
    state["authorized_turn"] = AUTHORIZATION_TOKEN in prompt
    state["prompt_at"] = time.time()
    save_state(session_id, state)


def strip_quoted(text: str) -> str:
    return re.sub(r"\"[^\"]*\"|'[^']*'", '""', text)


def strip_quoted_except_redirect_targets(text: str) -> str:
    """Apaga o conteúdo entre aspas (ex.: "Result<T>"), mas preserva o alvo citado logo depois de > ou >>."""
    return re.sub(
        r"(>{1,2}\s*)?(\"[^\"]*\"|'[^']*')",
        lambda match: match.group(0) if match.group(1) else '""',
        text,
    )


def split_segments(command: str) -> list[str]:
    """Trechos de um comando separados por ;, &&, ||, | e quebras de linha (fora de aspas, aproximadamente)."""
    return [segment for segment in re.split(r"\s*(?:&&|\|\||[;|\n])\s*", command) if segment.strip()]


def classify_shell(command: str, root: Path, cwd: str | None) -> str | None:
    # Um "git commit" sozinho (mensagem em aspas ou em heredoc) não altera arquivos da árvore de trabalho.
    commit_text = strip_quoted(HEREDOC_SUBSTITUTION_PATTERN.sub('""', command))
    if GIT_COMMIT_ONLY_PATTERN.fullmatch(commit_text):
        return None

    verdicts: list[str] = []
    mentions_protected = bool(PROTECTED_PATH_PATTERN.search(command))
    mentions_gated = bool(GATED_PATH_PATTERN.search(command))

    if ENCODED_COMMAND_PATTERN.search(command):
        return "gated"

    # Código embutido num interpretador: avaliado no comando inteiro (o heredoc atravessa as linhas).
    if INLINE_INTERPRETER_PATTERN.search(command) and INLINE_CODE_WRITE_PATTERN.search(command):
        if PROTECTED_PATH_PATTERN.search(command):
            verdicts.append("protected")
        elif GATED_PATH_PATTERN.search(command):
            verdicts.append("gated")

    current_dir: Path = Path(cwd) if cwd else root
    for segment in split_segments(command):
        cd = CD_PATTERN.match(segment)
        if cd:
            target = Path(cd.group(3).strip())
            current_dir = target if target.is_absolute() else current_dir / target
            continue

        segment_dir_kind = classify_relative(relative_to_root(str(current_dir), root, root))
        unquoted = strip_quoted_except_redirect_targets(segment)

        # Redirecionamentos e comandos de saída: decide pelo alvo.
        targets = [m.group(1) for m in REDIRECT_TARGET_PATTERN.finditer(unquoted)]
        targets += [m.group(4) for m in TEE_TARGET_PATTERN.finditer(segment)]
        targets += [m.group(4) for m in OUT_FILE_TARGET_PATTERN.finditer(segment)]
        for target in targets:
            if target.strip("'\"").lower() in NULL_TARGETS:
                continue
            if STATE_DIR_NAME in target.lower():
                verdicts.append("protected")
                continue
            if DYNAMIC_TARGET_PATTERN.match(target):
                # Alvo em variável: decide pelo que o comando inteiro cita (ex.: f=src/a.cs; echo x > $f).
                if mentions_protected:
                    verdicts.append("protected")
                elif mentions_gated or segment_dir_kind:
                    verdicts.append(segment_dir_kind or "gated")
                continue
            kind = classify_relative(relative_to_root(target, root, current_dir))
            if kind:
                verdicts.append(kind)

        # Comandos que escrevem no que citam, ou que mudam a árvore inteira.
        writes = any(pattern.search(segment) for pattern in WRITE_COMMAND_PATTERNS)
        if writes:
            uses_variable = re.search(r"[$%`]", strip_quoted(segment)) or re.search(r"\$\w|\$\{|\$\(", segment)
            if PROTECTED_PATH_PATTERN.search(segment) or (uses_variable and mentions_protected):
                verdicts.append("protected")
            elif GATED_PATH_PATTERN.search(segment) or segment_dir_kind or (uses_variable and mentions_gated):
                verdicts.append(segment_dir_kind or "gated")
        if TREE_CHANGING_PATTERN.search(segment):
            verdicts.append("gated")

    if "protected" in verdicts:
        return "protected"
    return "gated" if "gated" in verdicts else None


def classify_tool_call(payload: dict, root: Path) -> str | None:
    """Devolve 'protected', 'gated' ou None (escrita livre ou não é escrita)."""
    tool = payload.get("tool_name", "")
    tool_input = payload.get("tool_input") or {}
    cwd = payload.get("cwd")

    if tool in ("Edit", "Write", "MultiEdit", "NotebookEdit"):
        target = tool_input.get("file_path") or tool_input.get("notebook_path") or ""
        if STATE_DIR_NAME in target.replace("\\", "/").lower():
            return "protected"
        return classify_relative(relative_to_root(target, root, cwd))

    if tool in ("Bash", "PowerShell"):
        return classify_shell(tool_input.get("command") or "", root, cwd)

    return None


def deny(reason: str) -> None:
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "deny",
            "permissionDecisionReason": reason,
        }
    }, ensure_ascii=False))


def handle_tool(payload: dict) -> None:
    root = project_root(payload)
    kind = classify_tool_call(payload, root)
    if kind is None:
        return
    session_id = payload.get("session_id", "")
    state = load_state(session_id)
    if turn_is_authorized(state):
        return
    if kind == "protected":
        deny(DENY_REASON_PROTECTED)
        return
    spec_ref = state.get("spec_implement")
    if spec_ref and spec_allows_implementation(root, spec_ref):
        return
    if spec_ref:  # a spec saiu de "Em implementação": a sessão deixa de estar em implementação
        state.pop("spec_implement", None)
        save_state(session_id, state)
    deny(DENY_REASON_GATED)


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    event = sys.argv[1] if len(sys.argv) > 1 else ""
    raw = sys.stdin.buffer.read().decode("utf-8", errors="replace")
    try:
        payload = json.loads(raw) if raw.strip() else {}
    except ValueError:
        if event == "tool":  # sem saber qual é a ferramenta, o portão bloqueia
            deny("Portão SDD: dados do hook inválidos; escrita bloqueada por segurança.")
        return 0
    try:
        if event == "prompt":
            handle_prompt(payload)
        elif event == "tool":
            handle_tool(payload)
    except Exception as error:  # noqa: BLE001 - em caso de falha, bloqueia quando a ferramenta pode escrever
        if event == "tool" and re.search(r'"tool_name"\s*:\s*"(Edit|Write|MultiEdit|NotebookEdit|Bash|PowerShell)"', raw):
            deny(f"Portão SDD: falha interna ({type(error).__name__}); escrita bloqueada por segurança.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
