namespace NotasDesktop;

/// <summary>Janela de Localizar / Substituir de uma nota.</summary>
sealed class FindReplaceForm : Form
{
    readonly NoteForm nota;
    readonly TextBox txtBusca, txtSubstituir;
    readonly Label lblSubstituir;
    readonly CheckBox chkCaso;
    readonly Button btnProxima, btnSubstituir, btnTudo;

    public FindReplaceForm(NoteForm nota)
    {
        this.nota = nota;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9f);

        Label Rotulo(string t) => new() { Text = t, AutoSize = true, Anchor = AnchorStyles.Left };
        Button Botao(string t, Action acao)
        {
            var b = new Button { Text = t, AutoSize = true, Dock = DockStyle.Fill };
            b.Click += (_, _) => acao();
            return b;
        }

        txtBusca = new TextBox { Width = LogicalToDeviceUnits(220), Anchor = AnchorStyles.Left | AnchorStyles.Right };
        txtSubstituir = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        lblSubstituir = Rotulo("Substituir por:");
        chkCaso = new CheckBox { Text = "Diferenciar maiúsculas/minúsculas", AutoSize = true };

        btnProxima = Botao("Localizar próxima", () =>
        {
            if (txtBusca.Text.Length > 0) nota.LocalizarProxima(txtBusca.Text, chkCaso.Checked);
        });
        btnSubstituir = Botao("Substituir", () =>
        {
            if (txtBusca.Text.Length > 0) nota.Substituir(txtBusca.Text, txtSubstituir.Text, chkCaso.Checked);
        });
        btnTudo = Botao("Substituir tudo", () =>
        {
            if (txtBusca.Text.Length > 0) nota.SubstituirTudo(txtBusca.Text, txtSubstituir.Text, chkCaso.Checked);
        });

        var grade = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Padding = new Padding(8) };
        grade.Controls.Add(Rotulo("Localizar:"), 0, 0);
        grade.Controls.Add(txtBusca, 1, 0);
        grade.Controls.Add(btnProxima, 2, 0);
        grade.Controls.Add(lblSubstituir, 0, 1);
        grade.Controls.Add(txtSubstituir, 1, 1);
        grade.Controls.Add(btnSubstituir, 2, 1);
        grade.Controls.Add(chkCaso, 0, 2);
        grade.SetColumnSpan(chkCaso, 2);
        grade.Controls.Add(btnTudo, 2, 2);
        Controls.Add(grade);

        AcceptButton = btnProxima;
        // Se o foco sair da busca para fora do app, a anotação também termina a edição.
        Deactivate += (_, _) => nota.VerificarFocoDepois();
    }

    public void Configurar(bool substituir, string termo, bool diferenciarCaso)
    {
        Text = substituir ? "Substituir" : "Localizar";
        lblSubstituir.Visible = txtSubstituir.Visible = btnSubstituir.Visible = btnTudo.Visible = substituir;
        if (termo.Length > 0) txtBusca.Text = termo;
        chkCaso.Checked = diferenciarCaso;
        txtBusca.SelectAll();
        txtBusca.Focus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
