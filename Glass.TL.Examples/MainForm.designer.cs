
namespace GlassTL.Examples
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.closeButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.phoneNumberTextBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.authCodeTextBox = new System.Windows.Forms.TextBox();
            this.verifyAuthCodeButton = new System.Windows.Forms.Button();
            this.loginButton = new System.Windows.Forms.Button();
            this.changeMethodLinkLabel = new System.Windows.Forms.LinkLabel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.logListBox = new System.Windows.Forms.ListBox();
            this.hideButton = new System.Windows.Forms.Button();
            this.deleteAccountButton = new System.Windows.Forms.Button();
            this.verifyNameButton = new System.Windows.Forms.Button();
            this.firstNameTextBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.lastNameTextBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.passwordTextBox = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.verifyPasswordButton = new System.Windows.Forms.Button();
            this.viewHintLinkLabel = new System.Windows.Forms.LinkLabel();
            this.RawUpdatesCheckbox = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // closeButton
            // 
            this.closeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.closeButton.Location = new System.Drawing.Point(12, 475);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(75, 23);
            this.closeButton.TabIndex = 10;
            this.closeButton.Text = "&Close";
            this.closeButton.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(81, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Phone Number:";
            // 
            // phoneNumberTextBox
            // 
            this.phoneNumberTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.phoneNumberTextBox.Enabled = false;
            this.phoneNumberTextBox.Location = new System.Drawing.Point(106, 14);
            this.phoneNumberTextBox.Name = "phoneNumberTextBox";
            this.phoneNumberTextBox.Size = new System.Drawing.Size(130, 20);
            this.phoneNumberTextBox.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 43);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(60, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "&Auth Code:";
            // 
            // authCodeTextBox
            // 
            this.authCodeTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.authCodeTextBox.Enabled = false;
            this.authCodeTextBox.Location = new System.Drawing.Point(106, 40);
            this.authCodeTextBox.Name = "authCodeTextBox";
            this.authCodeTextBox.Size = new System.Drawing.Size(130, 20);
            this.authCodeTextBox.TabIndex = 4;
            // 
            // verifyAuthCodeButton
            // 
            this.verifyAuthCodeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.verifyAuthCodeButton.Enabled = false;
            this.verifyAuthCodeButton.Location = new System.Drawing.Point(242, 38);
            this.verifyAuthCodeButton.Name = "verifyAuthCodeButton";
            this.verifyAuthCodeButton.Size = new System.Drawing.Size(72, 23);
            this.verifyAuthCodeButton.TabIndex = 5;
            this.verifyAuthCodeButton.Text = "&Verify";
            this.verifyAuthCodeButton.UseVisualStyleBackColor = true;
            this.verifyAuthCodeButton.Click += new System.EventHandler(this.VerifyAuthCodeButton_Click);
            // 
            // loginButton
            // 
            this.loginButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.loginButton.Enabled = false;
            this.loginButton.Location = new System.Drawing.Point(242, 12);
            this.loginButton.Name = "loginButton";
            this.loginButton.Size = new System.Drawing.Size(72, 23);
            this.loginButton.TabIndex = 2;
            this.loginButton.Text = "&Send";
            this.loginButton.UseVisualStyleBackColor = true;
            this.loginButton.Click += new System.EventHandler(this.LoginButton_Click);
            // 
            // changeMethodLinkLabel
            // 
            this.changeMethodLinkLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.changeMethodLinkLabel.AutoSize = true;
            this.changeMethodLinkLabel.Enabled = false;
            this.changeMethodLinkLabel.Location = new System.Drawing.Point(320, 43);
            this.changeMethodLinkLabel.Name = "changeMethodLinkLabel";
            this.changeMethodLinkLabel.Size = new System.Drawing.Size(101, 13);
            this.changeMethodLinkLabel.TabIndex = 6;
            this.changeMethodLinkLabel.TabStop = true;
            this.changeMethodLinkLabel.Text = "Try Another Method";
            this.changeMethodLinkLabel.VisitedLinkColor = System.Drawing.Color.Blue;
            this.changeMethodLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.ChangeMethodLinkLabel_LinkClicked);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.logListBox);
            this.groupBox1.Location = new System.Drawing.Point(12, 154);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(409, 315);
            this.groupBox1.TabIndex = 7;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Log";
            // 
            // logListBox
            // 
            this.logListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.logListBox.FormattingEnabled = true;
            this.logListBox.IntegralHeight = false;
            this.logListBox.Location = new System.Drawing.Point(6, 19);
            this.logListBox.Name = "logListBox";
            this.logListBox.Size = new System.Drawing.Size(397, 290);
            this.logListBox.TabIndex = 8;
            // 
            // hideButton
            // 
            this.hideButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.hideButton.Location = new System.Drawing.Point(347, 475);
            this.hideButton.Name = "hideButton";
            this.hideButton.Size = new System.Drawing.Size(75, 23);
            this.hideButton.TabIndex = 9;
            this.hideButton.Text = "&Hide";
            this.hideButton.UseVisualStyleBackColor = true;
            // 
            // deleteAccountButton
            // 
            this.deleteAccountButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.deleteAccountButton.Location = new System.Drawing.Point(93, 475);
            this.deleteAccountButton.Name = "deleteAccountButton";
            this.deleteAccountButton.Size = new System.Drawing.Size(89, 23);
            this.deleteAccountButton.TabIndex = 11;
            this.deleteAccountButton.Text = "Delete Account";
            this.deleteAccountButton.UseVisualStyleBackColor = true;
            this.deleteAccountButton.Click += new System.EventHandler(this.DeleteAccountButton_Click);
            // 
            // verifyNameButton
            // 
            this.verifyNameButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.verifyNameButton.Enabled = false;
            this.verifyNameButton.Location = new System.Drawing.Point(242, 96);
            this.verifyNameButton.Name = "verifyNameButton";
            this.verifyNameButton.Size = new System.Drawing.Size(72, 23);
            this.verifyNameButton.TabIndex = 14;
            this.verifyNameButton.Text = "&Verify";
            this.verifyNameButton.UseVisualStyleBackColor = true;
            this.verifyNameButton.Click += new System.EventHandler(this.VerifyNameButton_Click);
            // 
            // firstNameTextBox
            // 
            this.firstNameTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.firstNameTextBox.Enabled = false;
            this.firstNameTextBox.Location = new System.Drawing.Point(106, 69);
            this.firstNameTextBox.Name = "firstNameTextBox";
            this.firstNameTextBox.Size = new System.Drawing.Size(130, 20);
            this.firstNameTextBox.TabIndex = 13;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(14, 72);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(60, 13);
            this.label3.TabIndex = 12;
            this.label3.Text = "&First Name:";
            // 
            // lastNameTextBox
            // 
            this.lastNameTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lastNameTextBox.Enabled = false;
            this.lastNameTextBox.Location = new System.Drawing.Point(106, 98);
            this.lastNameTextBox.Name = "lastNameTextBox";
            this.lastNameTextBox.Size = new System.Drawing.Size(130, 20);
            this.lastNameTextBox.TabIndex = 16;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(14, 101);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(61, 13);
            this.label4.TabIndex = 15;
            this.label4.Text = "&Last Name:";
            // 
            // passwordTextBox
            // 
            this.passwordTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.passwordTextBox.Enabled = false;
            this.passwordTextBox.Location = new System.Drawing.Point(106, 127);
            this.passwordTextBox.Name = "passwordTextBox";
            this.passwordTextBox.Size = new System.Drawing.Size(130, 20);
            this.passwordTextBox.TabIndex = 19;
            this.passwordTextBox.UseSystemPasswordChar = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(14, 130);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(86, 13);
            this.label5.TabIndex = 18;
            this.label5.Text = "Cloud Password:";
            // 
            // verifyPasswordButton
            // 
            this.verifyPasswordButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.verifyPasswordButton.Enabled = false;
            this.verifyPasswordButton.Location = new System.Drawing.Point(242, 125);
            this.verifyPasswordButton.Name = "verifyPasswordButton";
            this.verifyPasswordButton.Size = new System.Drawing.Size(72, 23);
            this.verifyPasswordButton.TabIndex = 17;
            this.verifyPasswordButton.Text = "&Verify";
            this.verifyPasswordButton.UseVisualStyleBackColor = true;
            this.verifyPasswordButton.Click += new System.EventHandler(this.VerifyPasswordButton_Click);
            // 
            // viewHintLinkLabel
            // 
            this.viewHintLinkLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.viewHintLinkLabel.AutoSize = true;
            this.viewHintLinkLabel.Enabled = false;
            this.viewHintLinkLabel.Location = new System.Drawing.Point(320, 130);
            this.viewHintLinkLabel.Name = "viewHintLinkLabel";
            this.viewHintLinkLabel.Size = new System.Drawing.Size(48, 13);
            this.viewHintLinkLabel.TabIndex = 20;
            this.viewHintLinkLabel.TabStop = true;
            this.viewHintLinkLabel.Text = "See Hint";
            this.viewHintLinkLabel.VisitedLinkColor = System.Drawing.Color.Blue;
            this.viewHintLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.ViewHintLinkLabel_LinkClicked);
            // 
            // RawUpdatesCheckbox
            // 
            this.RawUpdatesCheckbox.AutoSize = true;
            this.RawUpdatesCheckbox.Checked = true;
            this.RawUpdatesCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.RawUpdatesCheckbox.Location = new System.Drawing.Point(188, 479);
            this.RawUpdatesCheckbox.Name = "RawUpdatesCheckbox";
            this.RawUpdatesCheckbox.Size = new System.Drawing.Size(115, 17);
            this.RawUpdatesCheckbox.TabIndex = 21;
            this.RawUpdatesCheckbox.Text = "Print Raw Updates";
            this.RawUpdatesCheckbox.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(433, 510);
            this.Controls.Add(this.RawUpdatesCheckbox);
            this.Controls.Add(this.viewHintLinkLabel);
            this.Controls.Add(this.passwordTextBox);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.verifyPasswordButton);
            this.Controls.Add(this.lastNameTextBox);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.verifyNameButton);
            this.Controls.Add(this.firstNameTextBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.deleteAccountButton);
            this.Controls.Add(this.hideButton);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.changeMethodLinkLabel);
            this.Controls.Add(this.loginButton);
            this.Controls.Add(this.verifyAuthCodeButton);
            this.Controls.Add(this.authCodeTextBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.phoneNumberTextBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.closeButton);
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "Murtagh";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button closeButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox phoneNumberTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox authCodeTextBox;
        private System.Windows.Forms.Button verifyAuthCodeButton;
        private System.Windows.Forms.Button loginButton;
        private System.Windows.Forms.LinkLabel changeMethodLinkLabel;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ListBox logListBox;
        private System.Windows.Forms.Button hideButton;
        private System.Windows.Forms.Button deleteAccountButton;
        private System.Windows.Forms.Button verifyNameButton;
        private System.Windows.Forms.TextBox firstNameTextBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox lastNameTextBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox passwordTextBox;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button verifyPasswordButton;
        private System.Windows.Forms.LinkLabel viewHintLinkLabel;
        private System.Windows.Forms.CheckBox RawUpdatesCheckbox;
    }
}