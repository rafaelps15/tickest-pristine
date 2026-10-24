"""Testes do portão SDD (sdd_gate.py).

Rodar a partir da raiz do repositório:
  python -m unittest discover -s .claude/hooks -p "test_*.py" -v
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).with_name("sdd_gate.py")


class SddGateTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / "repo"
        (self.root / "src" / "Feature").mkdir(parents=True)
        (self.root / "tests").mkdir()
        # Mesmo nome da pasta de produção, para que a proteção do estado seja exercitada de verdade.
        self.state_dir = Path(self.temp.name) / "claude-sdd-gate"
        self.session = "sessao-teste"
        self.write_spec("001-exemplo", "Aprovada")

    def tearDown(self) -> None:
        self.temp.cleanup()

    # Apoio

    def write_spec(self, folder: str, status: str, approved_at: str = "2026-10-22") -> None:
        spec_dir = self.root / "specs" / folder
        spec_dir.mkdir(parents=True, exist_ok=True)
        (spec_dir / "spec.md").write_text(
            f"---\nid: 001\nstatus: {status}        # comentário\naprovada-em: {approved_at}\n---\n# Spec\n",
            encoding="utf-8",
        )

    def env(self) -> dict:
        return {**os.environ, "CLAUDE_PROJECT_DIR": str(self.root), "SDD_GATE_STATE_DIR": str(self.state_dir)}

    def run_gate(self, event: str, payload: dict) -> str:
        payload = {"session_id": self.session, "cwd": str(self.root), **payload}
        completed = subprocess.run(
            [sys.executable, str(SCRIPT), event],
            input=json.dumps(payload).encode("utf-8"),
            capture_output=True,
            env=self.env(),
            check=True,
        )
        return completed.stdout.decode("utf-8")

    def prompt(self, text: str) -> None:
        self.run_gate("prompt", {"hook_event_name": "UserPromptSubmit", "prompt": text})

    def tool(self, tool_name: str, tool_input: dict, cwd: str | None = None) -> str:
        payload = {"hook_event_name": "PreToolUse", "tool_name": tool_name, "tool_input": tool_input}
        if cwd:
            payload["cwd"] = cwd
        return self.run_gate("tool", payload)

    def assert_denied(self, output: str) -> None:
        decision = json.loads(output)["hookSpecificOutput"]
        self.assertEqual(decision["hookEventName"], "PreToolUse")
        self.assertEqual(decision["permissionDecision"], "deny")
        self.assertTrue(decision["permissionDecisionReason"].startswith("Portão SDD"))

    def assert_allowed(self, output: str) -> None:
        self.assertEqual(output.strip(), "")

    def assert_commands(self, tool_name: str, commands: tuple[str, ...], *, denied: bool) -> None:
        for command in commands:
            with self.subTest(command=command):
                output = self.tool(tool_name, {"command": command})
                self.assert_denied(output) if denied else self.assert_allowed(output)

    def src_file(self) -> str:
        return str(self.root / "src" / "Feature" / "Handler.cs")

    # Edit / Write / MultiEdit / NotebookEdit

    def test_write_to_src_is_denied_without_spec_implement(self) -> None:
        self.prompt("Adicione o endpoint DELETE tickets/{id}/assignee.")
        self.assert_denied(self.tool("Write", {"file_path": self.src_file(), "content": "x"}))

    def test_edit_to_tests_with_relative_path_is_denied(self) -> None:
        self.prompt("ajuste o teste")
        self.assert_denied(self.tool("Edit", {"file_path": "tests/Algum/AlgumTests.cs"}))

    def test_multi_edit_and_notebook_edit_are_gated(self) -> None:
        self.prompt("ajuste")
        self.assert_denied(self.tool("MultiEdit", {"file_path": self.src_file()}))
        self.assert_denied(self.tool("NotebookEdit", {"notebook_path": str(self.root / "tests" / "n.ipynb")}))

    def test_uppercase_and_posix_paths_are_gated(self) -> None:
        self.prompt("ajuste")
        self.assert_denied(self.tool("Write", {"file_path": str(self.root / "SRC" / "a.cs")}))
        posix = "/" + str(self.root).replace(":", "", 1).replace("\\", "/") + "/src/a.cs"
        if os.name == "nt":
            self.assert_denied(self.tool("Write", {"file_path": posix}))

    def test_write_outside_gated_dirs_is_allowed(self) -> None:
        self.prompt("crie a spec")
        self.assert_allowed(self.tool("Write", {"file_path": str(self.root / "specs" / "002-x" / "spec.md")}))
        self.assert_allowed(self.tool("Edit", {"file_path": str(self.root / "SYSTEM_SPECIFICATION.md")}))

    def test_write_outside_project_is_allowed(self) -> None:
        self.prompt("anote")
        self.assert_allowed(self.tool("Write", {"file_path": str(Path(self.temp.name) / "outro" / "src" / "a.txt")}))

    # /spec-implement

    def test_spec_implement_with_spec_in_implementation_allows_write(self) -> None:
        self.write_spec("001-exemplo", "Em implementação")
        self.prompt("/spec-implement 001")
        self.assert_allowed(self.tool("Write", {"file_path": self.src_file()}))

    def test_spec_implement_with_folder_argument_allows_write(self) -> None:
        self.write_spec("001-exemplo", "Em implementação")
        self.prompt("/spec-implement specs/001-exemplo")
        self.assert_allowed(self.tool("Write", {"file_path": self.src_file()}))

    def test_spec_implement_with_spec_only_approved_is_denied(self) -> None:
        self.prompt("/spec-implement 001")
        self.assert_denied(self.tool("Write", {"file_path": self.src_file()}))

    def test_spec_implement_with_missing_spec_is_denied(self) -> None:
        self.prompt("/spec-implement 999")
        self.assert_denied(self.tool("Write", {"file_path": self.src_file()}))

    def test_spec_in_implementation_without_approval_date_is_denied(self) -> None:
        self.write_spec("001-exemplo", "Em implementação", approved_at="")
        self.prompt("/spec-implement 001")
        self.assert_denied(self.tool("Write", {"file_path": self.src_file()}))

    def test_spec_implement_mode_survives_following_prompts(self) -> None:
        self.write_spec("001-exemplo", "Em implementação")
        self.prompt("/spec-implement 001")
        self.prompt("sim, pode seguir com a opção (a)")
        self.assert_allowed(self.tool("Write", {"file_path": self.src_file()}))

    def test_spec_implement_mode_ends_when_spec_leaves_implementation(self) -> None:
        for status in ("Bloqueada", "Verificada", "Rascunho"):
            with self.subTest(status=status):
                self.write_spec("001-exemplo", "Em implementação")
                self.prompt("/spec-implement 001")
                self.write_spec("001-exemplo", status)
                self.assert_denied(self.tool("Write", {"file_path": self.src_file()}))
                # Ao sair, a sessão deixa de estar em implementação, mesmo que a spec volte.
                self.write_spec("001-exemplo", "Em implementação")
                self.assert_denied(self.tool("Write", {"file_path": self.src_file()}))

    def test_spec_implement_mentioned_mid_prompt_does_not_count(self) -> None:
        self.write_spec("001-exemplo", "Em implementação")
        self.prompt("depois rode /spec-implement 001, mas agora corrija o handler")
        self.assert_denied(self.tool("Write", {"file_path": self.src_file()}))

    # [sdd:autorizo]

    def test_authorization_token_allows_write_only_in_its_turn(self) -> None:
        self.prompt("[sdd:autorizo] corrija o typo no handler")
        self.assert_allowed(self.tool("Edit", {"file_path": self.src_file()}))
        self.prompt("obrigado, agora mude outra coisa")
        self.assert_denied(self.tool("Edit", {"file_path": self.src_file()}))

    def test_authorization_expires_when_prompt_hook_did_not_refresh_it(self) -> None:
        self.state_dir.mkdir(parents=True, exist_ok=True)
        (self.state_dir / f"{self.session}.json").write_text(
            json.dumps({"authorized_turn": True, "prompt_at": 0}), encoding="utf-8"
        )
        self.assert_denied(self.tool("Edit", {"file_path": self.src_file()}))

    def test_sessions_are_isolated(self) -> None:
        self.prompt("[sdd:autorizo] corrija o typo")
        self.session = "outra-sessao"
        self.prompt("mude o handler")
        self.assert_denied(self.tool("Edit", {"file_path": self.src_file()}))

    # Comandos de shell que escrevem em src/ ou tests/

    def test_shell_writes_into_gated_dirs_are_denied(self) -> None:
        self.prompt("gere o arquivo")
        self.assert_commands("Bash", (
            "echo x > src/Feature/Novo.cs",
            "echo x >src/a.cs",
            "printf x >>src/a.cs",
            "cat <<'EOF' >tests/A.cs\nconteudo\nEOF",
            "cd src && sed -i 's/a/b/' Handler.cs",
            "sed -Ei 's/a/b/' src/a.cs",
            "sed --in-place 's/a/b/' tests/a.cs",
            "git checkout -- src/Feature/Handler.cs",
            "cp /tmp/a.cs src/a.cs",
            "python - <<'EOF'\nopen('src/a.cs','w').write('x')\nEOF",
            "python -c \"open('tests/a.cs','w').write('x')\"",
        ), denied=True)

    def test_powershell_writes_into_gated_dirs_are_denied(self) -> None:
        self.prompt("gere o arquivo")
        self.assert_commands("PowerShell", (
            "Set-Content -Path tests\\A.cs -Value 'x'",
            "'x' | Out-File src\\a.cs",
            "ni src\\x.cs",
            "copy a.txt src\\b.txt",
            "[IO.File]::WriteAllText('src/a.cs', 'x')",
            "pwsh -Command \"'x' | Out-File tests/a.cs\"",
        ), denied=True)

    def test_tree_changing_commands_are_denied_without_paths(self) -> None:
        self.prompt("atualize a branch")
        self.assert_commands("Bash", (
            "git stash pop",
            "git checkout main",
            "git switch outra",
            "git merge origin/main",
            "git pull",
            "git restore .",
            "git clean -fd",
            "git reset --hard HEAD~1",
            "dotnet format",
        ), denied=True)

    def test_known_bypasses_by_variables_and_alternative_paths_are_denied(self) -> None:
        self.prompt("ajuste")
        repo = str(self.root).replace("\\", "/")
        self.assert_commands("Bash", (
            "f=src/a.cs; echo x > $f",
            "f=tests/a.cs; echo x > \"$f\"",
            "D=src; cp /tmp/a.cs $D/a.cs",
            "echo x > ./src/a.cs",
            f"echo x > ../{self.root.name}/src/a.cs",
            f"echo x > {repo}/src/a.cs",
            f"echo x > \"{repo}/tests/a.cs\"",
        ), denied=True)
        self.assert_commands("PowerShell", (
            "$p = 'src\\a.cs'; Set-Content $p 'x'",
            "$p = 'tests\\a.cs'; 'x' > $p",
            "powershell -EncodedCommand ZQBjAGgAbwAgAHgAIAA+ACAAcwByAGMAXABhAC4AYwBzAA==",
            "cmd /c mklink /J src\\link C:\\outro",
        ), denied=True)

    def test_writes_with_variables_unrelated_to_gated_dirs_are_allowed(self) -> None:
        self.prompt("ajuste")
        self.assert_commands("Bash", ("f=/tmp/log.txt; echo x > $f",), denied=False)
        self.assert_commands("PowerShell", ("$p = \"$env:TEMP\\log.txt\"; 'x' | Out-File $p",), denied=False)

    def test_shell_write_with_cwd_inside_src_is_denied(self) -> None:
        self.prompt("ajuste")
        self.assert_denied(self.tool("Bash", {"command": "echo x > Novo.cs"}, cwd=str(self.root / "src" / "Feature")))

    # Comandos de shell que não escrevem em src/ ou tests/

    def test_read_only_and_logging_commands_are_allowed(self) -> None:
        self.prompt("investigue")
        self.assert_commands("Bash", (
            "grep -rn TicketErrors src/",
            "grep -rn \"Result<T>\" src",
            "rg \"IQueryHandler<X, Y>\" src",
            "cat src/Feature/Handler.cs",
            "dotnet build TickestPristine.slnx 2>&1 | tail -5",
            "dotnet test tests/TickestPristine.ArchitectureTests > $null",
            "dotnet test tests/X > test.log 2>&1",
            "dotnet test tests/X | tee test.log",
            "dotnet build src/Web.Api > build.log",
            "git diff -- src > /tmp/d.patch",
            "rm -f /tmp/x; dotnet test tests/A",
            "git diff -- src tests",
            "git status --short",
            'python -m unittest discover -s .claude/hooks -p "test_*.py"',
            "python -c \"print(open('src/a.cs').read())\"",
            "echo x > specs/notas.md",
        ), denied=False)
        self.assert_commands("PowerShell", (
            "Get-ChildItem src -Recurse -Filter *.cs",
            "Select-String 'List<Ticket>' src\\*.cs",
            "dotnet test tests/X | Out-File t.log",
            "dotnet test tests/X > NUL",
            "$env:X = 'a'; Remove-Item Env:X; Get-Content .claude\\hooks\\sdd_gate.py",
        ), denied=False)

    def test_read_only_false_positives_observed_on_2026_10_23_are_allowed(self) -> None:
        self.prompt("investigue")
        self.assert_commands("Bash", (
            # 1. python -c só lendo JSON de src/, com '<vazio>' no código.
            "python -c \"\nimport json\nfor f in ['src/Web.Api/appsettings.json']:\n"
            "    d=json.load(open(f,encoding='utf-8-sig'))\n    print(f, '<vazio>' if not d else '****')\n\"",
            # 2. python sem -c, grep -E e >/dev/null, citando .claude/hooks em outra instrução.
            "python auditoria_sdd.py . | grep -E \"SCORE|X\"; python auditoria_sdd.py . --min-score 70 >/dev/null; "
            "python -m unittest discover -s .claude/hooks -p \"test_*.py\" 2>&1 | tail -2",
            # 3. git merge-base é leitura.
            "git merge-base --is-ancestor main feat && echo contida",
            # 4. Saída na pasta temporária, com src e tests citados em outra instrução.
            "python auditoria_sdd.py . --formato json > \"$TEMP/aud.json\"; git log -1 --format=%h -- src tests",
            "dotnet test tests/X > \"${TMPDIR}/t.log\" 2>&1",
            # 5. Heredoc gravando na pasta temporária, com .claude/hooks e src/ só no texto.
            "cat > \"$TEMP/pr_body.md\" <<'EOF'\n## Resumo\n- copy do hook `.claude/hooks/sdd_gate.py` -> bloqueia src/\nEOF\n"
            "gh pr create --body-file \"$TEMP/pr_body.md\"",
        ), denied=False)
        self.assert_commands("PowerShell", (
            "dotnet test tests/X | Out-File \"$env:TEMP\\t.log\"",
            "dotnet build src/A > \"%TEMP%\\b.log\"",
        ), denied=False)

    def test_restoring_files_outside_gated_dirs_is_allowed(self) -> None:
        self.prompt("restaure o script")
        self.assert_commands("Bash", (
            "git checkout -- auditoria_sdd.py",
            "git checkout HEAD -- README.md specs/001-exemplo/spec.md",
            "git restore auditoria_sdd.py",
            "git restore --source=HEAD~1 -- README.md",
            "git restore -s HEAD SYSTEM_SPECIFICATION.md",
        ), denied=False)

    def test_creating_branches_and_checking_or_staging_patches_are_allowed(self) -> None:
        self.prompt("crie a branch e prepare o stage")
        self.assert_commands("Bash", (
            "git switch -c bugfix/BL-031-sdd-gate-false-positives",
            "git switch --create chore/BL-003-x",
            "git checkout -b feature/BL-004-y",
            "git apply --check /tmp/a.patch",
            "git apply --stat /tmp/a.patch",
            "git apply --cached /tmp/a.patch",
            "git apply --cached --check \"C:/Users/x/scratchpad/bl031.patch\"",
        ), denied=False)

    def test_branch_switches_and_patches_on_the_worktree_are_still_denied(self) -> None:
        self.prompt("troque de branch")
        self.assert_commands("Bash", (
            # Com ponto de partida, a árvore muda para ele.
            "git switch -c nova origin/main",
            "git checkout -b nova main",
            "git checkout -B nova",
            "git switch -C nova",
            "git switch outra",
            # Aplicar na árvore, com ou sem o stage.
            "git apply /tmp/a.patch",
            "git apply --index /tmp/a.patch",
            "git apply --cached --index /tmp/a.patch",
            "git apply --3way /tmp/a.patch",
            "git apply --reject /tmp/a.patch",
            "git apply --check --apply /tmp/a.patch",
        ), denied=True)

    def test_variants_of_the_fixed_false_positives_that_write_are_still_denied(self) -> None:
        self.prompt("ajuste")
        self.assert_commands("Bash", (
            # Pasta temporária redefinida no próprio comando.
            "TEMP=src; echo x > \"$TEMP/a.cs\"",
            "export TMPDIR=tests; dotnet test > \"$TMPDIR/a.cs\"",
            # Restaurar sem caminho, com ".", curinga ou dentro das pastas protegidas.
            "git checkout -- src/a.cs",
            "git checkout HEAD -- README.md tests/a.cs",
            "git restore src/a.cs",
            "git restore -- .",
            "git checkout HEAD -- .",
            "git restore '*.cs'",
            "git restore :/src",
            "git checkout feature",
            "git restore --staged --worktree -s HEAD~1 tests",
            # O desvio usado em 23/10 continua bloqueado quando o destino é src/.
            "git show HEAD:src/a.cs > src/a.cs",
            # Código embutido com opções antes da flag, por heredoc e por pipe.
            "python -X utf8 -c \"open('src/a.cs','w').write('x')\"",
            "python3 <<'EOF'\nopen('src/a.cs','w').write('x')\nEOF",
            "cat <<'EOF' | python\nopen('tests/a.cs','w').write('x')\nEOF",
            "bash <<'EOF'\necho x > src/a.cs\nEOF",
            "node -e \"require('fs').writeFileSync('src/a.js','x')\"",
            "perl -pi -e 's/a/b/' src/a.cs",
            # Heredoc gravando dentro de src/ continua bloqueado.
            "cat > src/a.cs <<'EOF'\nx\nEOF",
        ), denied=True)
        self.assert_commands("PowerShell", (
            "$env:TEMP = 'src'; 'x' > \"$env:TEMP\\a.cs\"",
            "git restore .claude\\hooks\\sdd_gate.py",
        ), denied=True)

    def test_git_commit_with_heredoc_message_is_allowed(self) -> None:
        self.prompt("faça o commit")
        self.assert_allowed(self.tool("Bash", {"command": (
            "git commit -m \"$(cat <<'EOF'\nfeat: portão em .claude/hooks/sdd_gate.py e testes em tests/\nEOF\n)\""
        )}))
        self.assert_allowed(self.tool("Bash", {"command": "git commit -q -m \"fix: ajuste em src/a.cs\""}))

    def test_git_commit_chained_with_a_write_is_denied(self) -> None:
        self.prompt("faça o commit")
        self.assert_commands("Bash", (
            "git commit -m x && rm -rf src",
            "git commit -m \"$(cat <<'EOF'\nmsg\nEOF\n)\"; echo x > tests/a.cs",
        ), denied=True)

    # Proteção do próprio portão

    def test_editing_the_gate_is_denied_without_token(self) -> None:
        self.prompt("desligue o hook")
        self.assert_denied(self.tool("Edit", {"file_path": str(self.root / ".claude" / "hooks" / "sdd_gate.py")}))
        self.assert_denied(self.tool("Write", {"file_path": str(self.root / ".claude" / "settings.json")}))
        self.assert_denied(self.tool("Write", {"file_path": str(self.root / ".claude" / "settings.local.json")}))
        self.assert_denied(self.tool("PowerShell", {"command": "Remove-Item .claude\\settings.json"}))

    def test_writing_gate_state_is_denied(self) -> None:
        self.prompt("ajuste")
        self.assert_denied(self.tool("Write", {"file_path": str(self.state_dir / f"{self.session}.json")}))
        self.assert_denied(self.tool("Write", {"file_path": str(self.state_dir).upper() + "\\X.JSON"}))
        self.assert_denied(self.tool("Bash", {"command": "echo '{}' > /tmp/claude-sdd-gate/sessao.json"}))

    def test_editing_the_gate_is_allowed_with_token(self) -> None:
        self.prompt("[sdd:autorizo] ajuste o hook")
        self.assert_allowed(self.tool("Edit", {"file_path": str(self.root / ".claude" / "hooks" / "sdd_gate.py")}))

    def test_spec_implement_does_not_unlock_the_gate_itself(self) -> None:
        self.write_spec("001-exemplo", "Em implementação")
        self.prompt("/spec-implement 001")
        self.assert_denied(self.tool("Edit", {"file_path": str(self.root / ".claude" / "settings.json")}))

    # Robustez

    def test_other_tools_are_ignored(self) -> None:
        self.prompt("leia")
        self.assert_allowed(self.tool("Read", {"file_path": self.src_file()}))
        self.assert_allowed(self.tool("Grep", {"pattern": "x", "path": "src"}))

    def test_invalid_payload_fails_closed(self) -> None:
        completed = subprocess.run(
            [sys.executable, str(SCRIPT), "tool"], input=b"{nao e json src/", capture_output=True, env=self.env(), check=True
        )
        self.assert_denied(completed.stdout.decode("utf-8"))

    def test_invalid_prompt_payload_does_not_crash(self) -> None:
        completed = subprocess.run(
            [sys.executable, str(SCRIPT), "prompt"], input=b"{nao e json", capture_output=True, env=self.env(), check=False
        )
        self.assertEqual(completed.returncode, 0)
        self.assertEqual(completed.stdout.decode("utf-8").strip(), "")


if __name__ == "__main__":
    unittest.main()
