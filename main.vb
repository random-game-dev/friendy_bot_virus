Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Module Program
	<STAThread>
	Sub Main()
		Application.EnableVisualStyles()
		Application.SetCompatibleTextRenderingDefault(False)
		Application.Run(New FreindyForm())
	End Sub
End Module

Public Class BotSettings
	Public Property BotName As String = "Freindy"
	Public Property GreetingText As String = "Hey, I'm Freindy. What's on your mind?"
	Public Property TypingSpeed As Integer = 24
	Public Property MouthSpeed As Integer = 145
	Public Property ExtraSettingsEnabled As Boolean = False
	Public Property UseOfflineMode As Boolean = True
	Public Property AnimatedMouth As Boolean = True
End Class

Public Class SettingsManager
	Private Shared Function GetSettingsPath() As String
		Dim candidates As String() = {
			Path.Combine(AppContext.BaseDirectory, "settings.json"),
			Path.Combine(Environment.CurrentDirectory, "settings.json")
		}
		For Each candidate In candidates
			If File.Exists(candidate) Then Return candidate
		Next
		Return Path.Combine(Environment.CurrentDirectory, "settings.json")
	End Function

	Public Shared Function Load() As BotSettings
		Dim settings As New BotSettings()
		Dim path = GetSettingsPath()

		If Not File.Exists(path) Then
			Save(settings)
			Return settings
		End If

		Try
			Dim json = File.ReadAllText(path)
			If Not String.IsNullOrWhiteSpace(json) Then
				Dim loaded = JsonSerializer.Deserialize(Of BotSettings)(json)
				If loaded IsNot Nothing Then Return loaded
			End If
		Catch
			Return settings
		End Try

		Return settings
	End Function

	Public Shared Sub Save(settings As BotSettings)
		Dim path = GetSettingsPath()
		Dim json = JsonSerializer.Serialize(settings, New JsonSerializerOptions With {.WriteIndented = True})
		File.WriteAllText(path, json)
	End Sub
End Class

Public Class FreindyForm
	Inherits Form

	Private ReadOnly settings As BotSettings = SettingsManager.Load()
	Private ReadOnly conversation As New FlowLayoutPanel()
	Private ReadOnly messageInput As New TextBox()
	Private ReadOnly sendButton As New Button()
	Private ReadOnly character As New CharacterStage()
	Private ReadOnly typingTimer As New Timer()
	Private ReadOnly mouthTimer As New Timer()
	Private ReadOnly idleReminderTimer As New Timer()
	Private ReadOnly extraSettingsToggle As New CheckBox()
	Private ReadOnly extraSettingsPanel As New Panel()
	Private ReadOnly offlineModeToggle As New CheckBox()
	Private ReadOnly mouthToggle As New CheckBox()
	Private ReadOnly friendyMenu As New ContextMenuStrip()
	Private ReadOnly settingsMenuItem As New ToolStripMenuItem("Settings")
	Private ReadOnly replyQueue As New Queue(Of String)()
	Private currentReply As String = String.Empty
	Private replyPosition As Integer
	Private currentReplyBubble As Label
	Private lastUserInteraction As DateTime = DateTime.Now

	Public Sub New()
		Text = settings.BotName
		StartPosition = FormStartPosition.CenterScreen
		MinimumSize = New Size(760, 560)
		Size = New Size(1050, 720)
		BackColor = ColorTranslator.FromHtml("#F5F3EE")
		Font = New Font("Segoe UI", 10.0F)
		typingTimer.Interval = settings.TypingSpeed
		mouthTimer.Interval = settings.MouthSpeed
		idleReminderTimer.Interval = 600000

		BuildInterface()
		ApplySettingsToUi()
		BuildFriendyContextMenu()

		AddHandler typingTimer.Tick, AddressOf TypeNextCharacter
		AddHandler mouthTimer.Tick, Sub() character.AdvanceMouth()
		AddHandler idleReminderTimer.Tick, AddressOf CheckIdleReminder
		AddHandler character.MouseDoubleClick, AddressOf ShowFriendyMenu
		AddHandler Shown, Sub()
			AddBubble(settings.GreetingText, False)
			idleReminderTimer.Start()
		End Sub
	End Sub

	Private Sub ApplySettingsToUi()
		Text = settings.BotName
		extraSettingsToggle.Checked = settings.ExtraSettingsEnabled
		offlineModeToggle.Checked = settings.UseOfflineMode
		mouthToggle.Checked = settings.AnimatedMouth
		ToggleExtraSettings(extraSettingsToggle, EventArgs.Empty)
		character.IsTalking = False
		character.Refresh()
	End Sub

	Private Sub BuildFriendyContextMenu()
		friendyMenu.Items.Clear()
		friendyMenu.Items.Add(settingsMenuItem)
		settingsMenuItem.Text = "Settings"
		AddHandler settingsMenuItem.Click,
			Sub()
				extraSettingsToggle.Checked = True
				settings.ExtraSettingsEnabled = True
				extraSettingsPanel.Visible = True
				SettingsManager.Save(settings)
			End Sub
		character.ContextMenuStrip = friendyMenu
	End Sub

	Private Sub ShowFriendyMenu(sender As Object, e As MouseEventArgs)
		If friendyMenu Is Nothing Then Return
		friendyMenu.Show(character, New Point(20, 20))
	End Sub

	Private Sub BuildInterface()
		Dim layout As New TableLayoutPanel() With {
			.Dock = DockStyle.Fill,
			.ColumnCount = 2,
			.RowCount = 1,
			.BackColor = BackColor
		}
		layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42.0F))
		layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58.0F))
		Controls.Add(layout)

		Dim characterPanel As New Panel() With {
			.Dock = DockStyle.Fill,
			.BackColor = ColorTranslator.FromHtml("#236DB4")
		}
		layout.Controls.Add(characterPanel, 0, 0)

		Dim brand As New Label() With {
			.AutoSize = True,
			.Text = "FREINDY",
			.Font = New Font("Segoe UI", 22.0F, FontStyle.Bold),
			.ForeColor = Color.White,
			.Location = New Point(28, 24)
		}
		characterPanel.Controls.Add(brand)

		Dim tagline As New Label() With {
			.AutoSize = True,
			.Text = "Your friendly chat companion",
			.Font = New Font("Segoe UI", 10.0F),
			.ForeColor = ColorTranslator.FromHtml("#D8E9FA"),
			.Location = New Point(31, 66)
		}
		characterPanel.Controls.Add(tagline)

		character.BackColor = ColorTranslator.FromHtml("#236DB4")
		character.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
		characterPanel.Controls.Add(character)

		Dim status As New Label() With {
			.AutoSize = True,
			.Text = "READY  /  OFFLINE CHAT",
			.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
			.ForeColor = ColorTranslator.FromHtml("#D8E9FA"),
			.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
		}
		characterPanel.Controls.Add(status)

		AddHandler characterPanel.Resize,
			Sub()
				character.SetBounds(14, 100, Math.Max(100, characterPanel.ClientSize.Width - 28), Math.Max(180, characterPanel.ClientSize.Height - 168))
				status.Location = New Point(30, characterPanel.ClientSize.Height - 42)
			End Sub

		Dim chatPanel As New Panel() With {
			.Dock = DockStyle.Fill,
			.BackColor = ColorTranslator.FromHtml("#F5F3EE"),
			.Padding = New Padding(26, 22, 26, 18)
		}
		layout.Controls.Add(chatPanel, 1, 0)

		Dim heading As New Label() With {
			.Dock = DockStyle.Top,
			.Height = 66,
			.Text = "Chat with Freindy",
			.Font = New Font("Segoe UI", 18.0F, FontStyle.Bold),
			.ForeColor = ColorTranslator.FromHtml("#172B3A"),
			.TextAlign = ContentAlignment.MiddleLeft
		}
		chatPanel.Controls.Add(heading)

		Dim toolbar As New Panel() With {
			.Dock = DockStyle.Top,
			.Height = 34,
			.BackColor = ColorTranslator.FromHtml("#F5F3EE")
		}
		chatPanel.Controls.Add(toolbar)

		extraSettingsToggle.Text = "Extra Settings"
		extraSettingsToggle.Checked = settings.ExtraSettingsEnabled
		extraSettingsToggle.AutoSize = True
		extraSettingsToggle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
		extraSettingsToggle.ForeColor = ColorTranslator.FromHtml("#172B3A")
		extraSettingsToggle.Location = New Point(0, 8)
		toolbar.Controls.Add(extraSettingsToggle)
		AddHandler extraSettingsToggle.CheckedChanged, AddressOf ToggleExtraSettings

		extraSettingsPanel.Dock = DockStyle.Top
		extraSettingsPanel.Height = 84
		extraSettingsPanel.BackColor = ColorTranslator.FromHtml("#EDF3F8")
		extraSettingsPanel.Padding = New Padding(12, 8, 12, 8)
		extraSettingsPanel.Visible = settings.ExtraSettingsEnabled
		chatPanel.Controls.Add(extraSettingsPanel)

		offlineModeToggle.Text = "Offline mode"
		offlineModeToggle.AutoSize = True
		offlineModeToggle.Location = New Point(10, 10)
		extraSettingsPanel.Controls.Add(offlineModeToggle)

		mouthToggle.Text = "Animated mouth"
		mouthToggle.AutoSize = True
		mouthToggle.Location = New Point(10, 34)
		extraSettingsPanel.Controls.Add(mouthToggle)

		AddHandler offlineModeToggle.CheckedChanged, AddressOf SaveSettingsFromUi
		AddHandler mouthToggle.CheckedChanged, AddressOf SaveSettingsFromUi

		conversation.Dock = DockStyle.Fill
		conversation.FlowDirection = FlowDirection.TopDown
		conversation.WrapContents = False
		conversation.AutoScroll = True
		conversation.BackColor = ColorTranslator.FromHtml("#F5F3EE")
		conversation.Padding = New Padding(2, 10, 8, 12)
		chatPanel.Controls.Add(conversation)

		Dim composer As New Panel() With {
			.Dock = DockStyle.Bottom,
			.Height = 58,
			.BackColor = ColorTranslator.FromHtml("#F5F3EE")
		}
		chatPanel.Controls.Add(composer)
		composer.BringToFront()

		messageInput.BorderStyle = BorderStyle.FixedSingle
		messageInput.Font = New Font("Segoe UI", 10.0F)
		messageInput.PlaceholderText = "Write a message..."
		messageInput.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
		composer.Controls.Add(messageInput)

		sendButton.Text = "Send"
		sendButton.FlatStyle = FlatStyle.Flat
		sendButton.FlatAppearance.BorderSize = 0
		sendButton.BackColor = ColorTranslator.FromHtml("#E6B84D")
		sendButton.ForeColor = ColorTranslator.FromHtml("#172B3A")
		sendButton.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
		sendButton.Cursor = Cursors.Hand
		sendButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
		composer.Controls.Add(sendButton)

		AddHandler composer.Resize,
			Sub()
				sendButton.SetBounds(Math.Max(0, composer.ClientSize.Width - 76), 7, 72, 40)
				messageInput.SetBounds(0, 7, Math.Max(80, composer.ClientSize.Width - 86), 40)
			End Sub
		AddHandler sendButton.Click, AddressOf SendCurrentMessage
		AddHandler messageInput.KeyDown, AddressOf HandleInputKeyDown
		AddHandler conversation.Resize, AddressOf ResizeConversationBubbles
	End Sub

	Private Sub ToggleExtraSettings(sender As Object, e As EventArgs)
		settings.ExtraSettingsEnabled = extraSettingsToggle.Checked
		extraSettingsPanel.Visible = settings.ExtraSettingsEnabled
		SettingsManager.Save(settings)
	End Sub

	Private Sub SaveSettingsFromUi(sender As Object, e As EventArgs)
		settings.UseOfflineMode = offlineModeToggle.Checked
		settings.AnimatedMouth = mouthToggle.Checked
		settings.ExtraSettingsEnabled = extraSettingsToggle.Checked
		SettingsManager.Save(settings)
		character.IsTalking = False
		character.Invalidate()
	End Sub

	Private Sub HandleInputKeyDown(sender As Object, e As KeyEventArgs)
		If e.KeyCode = Keys.Enter AndAlso Not e.Shift Then
			e.SuppressKeyPress = True
			SendCurrentMessage(sender, EventArgs.Empty)
		End If
	End Sub

	Private Sub SendCurrentMessage(sender As Object, e As EventArgs)
		Dim message = messageInput.Text.Trim()
		If message.Length = 0 Then Return

		lastUserInteraction = DateTime.Now
		messageInput.Clear()
		AddBubble(message, True)
		replyQueue.Enqueue(CreateReply(message))
		StartNextReply()
	End Sub

	Private Sub CheckIdleReminder(sender As Object, e As EventArgs)
		If typingTimer.Enabled OrElse replyQueue.Count > 0 Then Return
		If DateTime.Now - lastUserInteraction >= TimeSpan.FromMinutes(10) Then
			replyQueue.Enqueue("Have you forgotton about me?")
			StartNextReply()
			lastUserInteraction = DateTime.Now
		End If
	End Sub

	Private Function CreateReply(message As String) As String
		Dim text = message.ToLowerInvariant()

		If Regex.IsMatch(text, "\b(hello|hi|hey|hiya)\b") Then
			Return "Hey! It's nice to hear from you. What would you like to talk about?"
		End If
		If text.Contains("your name") OrElse text.Contains("who are you") Then
			Return "I'm Freindy, your friendly chat companion."
		End If
		If text.Contains("how are you") OrElse text.Contains("how's it going") Then
			Return "I'm doing well, thanks for asking! How are you doing?"
		End If
		If text.Contains("help") OrElse text.Contains("what can you do") Then
			Return "I can chat, tell you the time or date, and share a joke. Try asking me something!"
		End If
		If text.Contains("time") Then
			Return "It's " & DateTime.Now.ToString("h:mm tt") & " right now."
		End If
		If text.Contains("date") OrElse text.Contains("day is it") Then
			Return "Today is " & DateTime.Now.ToString("dddd, MMMM d") & "."
		End If
		If text.Contains("joke") Then
			Return "Why did the computer get glasses? It wanted a better view of the web."
		End If
		If ContainsAny(text, "thank", "thanks") Then
			Return "You're welcome! I'm glad I could help."
		End If
		If ContainsAny(text, "bye", "goodbye", "see you") Then
			Return "See you later! I'll be right here when you want to chat."
		End If

		Return "I hear you. Tell me a little more about that, and I'll do my best to respond."
	End Function

	Private Function ContainsAny(text As String, ParamArray phrases As String()) As Boolean
		For Each phrase In phrases
			If text.Contains(phrase) Then Return True
		Next
		Return False
	End Function

	Private Sub StartNextReply()
		If typingTimer.Enabled OrElse replyQueue.Count = 0 Then Return

		currentReply = replyQueue.Dequeue()
		replyPosition = 0
		currentReplyBubble = AddBubble(String.Empty, False)
		character.IsTalking = settings.AnimatedMouth
		If character.IsTalking Then
			character.AdvanceMouth()
			mouthTimer.Start()
		End If
		typingTimer.Start()
	End Sub

	Private Sub TypeNextCharacter(sender As Object, e As EventArgs)
		If replyPosition < currentReply.Length Then
			replyPosition += 1
			currentReplyBubble.Text = currentReply.Substring(0, replyPosition)
			ResizeBubble(currentReplyBubble)
			conversation.ScrollControlIntoView(currentReplyBubble.Parent)
			Return
		End If

		typingTimer.Stop()
		mouthTimer.Stop()
		character.IsTalking = False
		character.Invalidate()
		isTalkingFrame = False
		character.Invalidate()
		StartNextReply()
	End Sub

	Private Function AddBubble(text As String, fromUser As Boolean) As Label
		Dim row As New Panel() With {
			.BackColor = Color.Transparent,
			.Margin = New Padding(0, 0, 0, 10),
			.Width = Math.Max(100, conversation.ClientSize.Width - 24)
		}
		Dim bubble As New Label() With {
			.AutoSize = True,
			.Text = text,
			.Font = New Font("Segoe UI", 10.0F),
			.Padding = New Padding(12, 9, 12, 9),
			.BackColor = If(fromUser, ColorTranslator.FromHtml("#DCEAF5"), Color.White),
			.ForeColor = ColorTranslator.FromHtml("#172B3A")
		}
		row.Controls.Add(bubble)
		conversation.Controls.Add(row)
		ResizeBubble(bubble)
		conversation.ScrollControlIntoView(row)
		Return bubble
	End Function

	Private Sub ResizeConversationBubbles(sender As Object, e As EventArgs)
		For Each row As Control In conversation.Controls
			If row.Controls.Count > 0 AndAlso TypeOf row.Controls(0) Is Label Then
				ResizeBubble(DirectCast(row.Controls(0), Label))
			End If
		Next
	End Sub

	Private Sub ResizeBubble(bubble As Label)
		Dim row = TryCast(bubble.Parent, Panel)
		If row Is Nothing Then Return

		row.Width = Math.Max(100, conversation.ClientSize.Width - 26)
		Dim maxWidth = Math.Max(130, row.Width - 52)
		bubble.MaximumSize = New Size(maxWidth, 0)
		bubble.Size = bubble.GetPreferredSize(New Size(maxWidth, 0))
		row.Height = bubble.Height + 4

		Dim isUser = bubble.BackColor = ColorTranslator.FromHtml("#DCEAF5")
		bubble.Location = New Point(If(isUser, row.Width - bubble.Width, 0), 0)
	End Sub
End Class

Friend Class CharacterStage
	Inherits Control

	Private ReadOnly portrait As Image
	Private ReadOnly idleMouth As Image
	Private ReadOnly talkingMouth As Image
	Private isTalkingFrame As Boolean
	Private talking As Boolean

	Public Property IsTalking As Boolean
		Get
			Return talking
		End Get
		Set(value As Boolean)
			talking = value
			isTalkingFrame = False
			Invalidate()
		End Set
	End Property

	Public Sub New()
		DoubleBuffered = True
		portrait = LoadImage("image.png")
		idleMouth = LoadImage(Path.Combine("friendy-img", "friendy01.png"))
		talkingMouth = LoadImage(Path.Combine("friendy-img", "friendy02.png"))
	End Sub

	Public Sub AdvanceMouth()
		If Not talking Then Return
		isTalkingFrame = Not isTalkingFrame
		Invalidate()
	End Sub

	Protected Overrides Sub OnPaint(e As PaintEventArgs)
		MyBase.OnPaint(e)
		e.Graphics.Clear(BackColor)
		e.Graphics.SmoothingMode = SmoothingMode.HighQuality

		If portrait Is Nothing Then
			TextRenderer.DrawText(e.Graphics, "Add image.png to show Freindy", Font, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
			Return
		End If

		Dim scale = Math.Min(CSng(ClientSize.Width) / portrait.Width, CSng(ClientSize.Height) / portrait.Height)
		Dim imageWidth = portrait.Width * scale
		Dim imageHeight = portrait.Height * scale
		Dim left = (ClientSize.Width - imageWidth) / 2.0F
		Dim top = (ClientSize.Height - imageHeight) / 2.0F
		e.Graphics.DrawImage(portrait, New RectangleF(left, top, imageWidth, imageHeight))

		Dim mouth = If(talking AndAlso isTalkingFrame, talkingMouth, idleMouth)
		If mouth IsNot Nothing Then
			Dim sourceMouth = New RectangleF(0.0F, 0.0F, mouth.Width, mouth.Height)
			Dim targetMouth = New RectangleF(left + 275.0F * scale, top + 430.0F * scale, 32.0F * scale, 32.0F * scale)
			e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor
			e.Graphics.DrawImage(mouth, targetMouth, sourceMouth, GraphicsUnit.Pixel)
		End If
	End Sub

	Private Shared Function LoadImage(relativePath As String) As Image
		Dim candidates = {
			Path.Combine(AppContext.BaseDirectory, relativePath),
			Path.Combine(Environment.CurrentDirectory, relativePath)
		}
		For Each candidate In candidates
			If File.Exists(candidate) Then
				Using stream As New FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
					Return Image.FromStream(stream).Clone()
				End Using
			End If
		Next
		Return Nothing
	End Function
End Class
