using OpenAI.Chat;

namespace DeepSeek.ToolBuilder
{
    public static class BookRecognitionToolBuilder
    {
        public static string FunctionName = "RecognizeBook";

        public static ChatTool Build()
        {
            return ChatTool.CreateFunctionTool(
                functionName: FunctionName,
                functionDescription: "Распознает информацию о книге из описания",
                functionParameters: BinaryData.FromBytes("""
                    {
                        "type": "object",
                        "properties": {
                            "imageDescription": {
                                "type": "string",
                                "description": "Описание изображения книги"
                            }
                        },
                        "required": ["imageDescription"]
                    }
                    """u8.ToArray())
            );
        }
    }
}
