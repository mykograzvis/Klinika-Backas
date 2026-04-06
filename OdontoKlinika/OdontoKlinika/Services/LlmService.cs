using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

public class LlmService
{
    private readonly ChatClient _chatClient;

    private static readonly ChatTool[] Tools =
    [
        ChatTool.CreateFunctionTool(
            "find_patient",
            "Ieško paciento pagal vardą ir pavardę. Grąžina paciento ID ir pilną vardą.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "name": { "type": "string", "description": "Paciento vardas, pavardė arba dalis" }
              },
              "required": ["name"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_patient_history",
            "Gauna paciento praėjusių vizitų istoriją. Gali filtruoti pagal procedūros tipą.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "patientId":      { "type": "integer" },
                "procedure_type": { "type": "string", "description": "Neprivalomas filtras, pvz. 'higiena'" }
              },
              "required": ["patientId"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_patient_visits",
            "Gauna paciento artėjančius (ateities) vizitus.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "patientId": { "type": "integer" }
              },
              "required": ["patientId"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_doctors",
            "Gauna gydytojų sąrašą. Gali filtruoti pagal specializaciją.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "specializacija": { "type": "string", "description": "Neprivalomas filtras, pvz. 'ortodontas', 'chirurgas'" }
              }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_free_slots",
            "Randa laisvus vizitų laikus pagal gydytoją ir procedūrą. Datos formatas YYYY-MM-DD.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "doctorName":  { "type": "string", "description": "Gydytojo vardas arba pavardė" },
                "serviceName": { "type": "string", "description": "Procedūros pavadinimas, pvz. 'higiena'" },
                "fromDate":    { "type": "string", "description": "Nuo datos YYYY-MM-DD" },
                "toDate":      { "type": "string", "description": "Iki datos YYYY-MM-DD" }
              },
              "required": ["doctorName", "serviceName"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "create_reservation",
            "Sukuria naują vizito rezervaciją pacientui. Kviesti TIK kai vartotojas patvirtino laiką.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "patientId":  { "type": "integer" },
                "doctorId":   { "type": "integer" },
                "serviceId":  { "type": "integer" },
                "datetime":   { "type": "string", "description": "Formatas: YYYY-MM-DD HH:mm" }
              },
              "required": ["patientId", "doctorId", "serviceId", "datetime"]
            }
            """)),
    ];

    public LlmService(IConfiguration config)
    {
        var endpoint = config["Llm:Endpoint"] ?? "http://localhost:1234/v1";
        var apiKey = config["Llm:ApiKey"] ?? "lm-studio";
        var model = config["Llm:Model"] ?? "local-model";

        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        _chatClient = client.GetChatClient(model);
    }

    public async Task<string> ChatWithToolsAsync(
        List<ChatMessage> history,
        string userMessage,
        Func<string, string, Task<string>> toolExecutor)
    {
        // Šiandienos data įtraukiama į system prompt kiekvienos užklausos metu
        var today = DateTime.Now;
        var dateStr = today.ToString("yyyy-MM-dd");
        var dayOfWeek = today.DayOfWeek switch
        {
            DayOfWeek.Monday => "pirmadienis",
            DayOfWeek.Tuesday => "antradienis",
            DayOfWeek.Wednesday => "trečiadienis",
            DayOfWeek.Thursday => "ketvirtadienis",
            DayOfWeek.Friday => "penktadienis",
            DayOfWeek.Saturday => "šeštadienis",
            DayOfWeek.Sunday => "sekmadienis",
            _ => today.DayOfWeek.ToString()
        };

        // Apskaičiuojam artimiausius savaitės dienas
        var nextMonday = today.AddDays((8 - (int)today.DayOfWeek) % 7).ToString("yyyy-MM-dd");
        var nextFriday = today.AddDays((12 - (int)today.DayOfWeek) % 7).ToString("yyyy-MM-dd");

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                $"Tu esi odontologijos klinikos valdymo sistemos asistentas. Atsakyk VISADA lietuvių kalba.\n" +
                $"ŠIANDIEN: {dateStr} ({dayOfWeek}). " +
                $"Ateinantis pirmadienis: {nextMonday}. Ateinantis penktadienis: {nextFriday}.\n" +
                $"Kai vartotojas sako 'šiandien' — naudok {dateStr}. " +
                $"Kai sako 'kitą penktadienį' — naudok {nextFriday}. " +
                $"Kai sako 'kitą pirmadienį' — naudok {nextMonday}.\n" +
                $"TAISYKLĖS:\n" +
                $"1. Visada naudok įrankius — niekada nespėk duomenų iš galvos.\n" +
                $"2. Jei nežinai paciento ID — pirmiausia iškvieski find_patient.\n" +
                $"3. Jei nežinai gydytojo ID — iškvieski get_doctors.\n" +
                $"4. Rezervaciją kurk tik kai vartotojas aiškiai patvirtino laiką.\n" +
                $"5. Grąžink tik tai ką rado įrankiai — nieko nepridėk iš savęs.")
        };

        messages.AddRange(history);
        messages.Add(new UserChatMessage(userMessage));

        var chatOptions = new ChatCompletionOptions();
        foreach (var tool in Tools)
            chatOptions.Tools.Add(tool);

        int maxIterations = 6;
        int iteration = 0;

        while (iteration++ < maxIterations)
        {
            var response = await _chatClient.CompleteChatAsync(messages, chatOptions);
            var completion = response.Value;

            if (completion.FinishReason == ChatFinishReason.ToolCalls
                && completion.ToolCalls.Count > 0)
            {
                messages.Add(new AssistantChatMessage(completion));

                foreach (var toolCall in completion.ToolCalls)
                {
                    string result;
                    try
                    {
                        result = await toolExecutor(
                            toolCall.FunctionName,
                            toolCall.FunctionArguments.ToString());
                    }
                    catch (Exception ex)
                    {
                        result = $"{{\"error\": \"{ex.Message}\"}}";
                    }

                    messages.Add(new ToolChatMessage(toolCall.Id, result));
                }
            }
            else
            {
                if (completion.Content == null || completion.Content.Count == 0)
                    return "Atsiprašau, negavau atsakymo iš modelio.";

                return StripThinking(completion.Content[0].Text);
            }
        }

        return "Atsiprašau, užklausa per sudėtinga. Pabandykite perfrazuoti.";
    }

    public async Task<string> ChatAsync(string message, string systemPrompt = "")
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(systemPrompt))
            messages.Add(new SystemChatMessage("/no_think\n" + systemPrompt));
        messages.Add(new UserChatMessage(message));

        var response = await _chatClient.CompleteChatAsync(messages);
        return StripThinking(response.Value.Content[0].Text);
    }

    private static string StripThinking(string text) =>
        System.Text.RegularExpressions.Regex.Replace(
            text,
            @"<think>[\s\S]*?</think>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        ).Trim();
}