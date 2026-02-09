using OpenAI.Chat;

namespace DeepSeekClient.ToolBuilder
{
    public static class CreateBookRecognitionToolBuilder
    {
        public static ChatTool Build()
        {
            return ChatTool.CreateFunctionTool(
                functionName: "RecognizeBook",
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
