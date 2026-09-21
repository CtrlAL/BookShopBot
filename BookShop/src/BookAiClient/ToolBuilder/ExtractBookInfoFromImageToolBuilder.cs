using OpenAI.Chat;

namespace BookAi.ToolBuilder
{
    public static class ExtractBookInfoFromImageToolBuilder
    {
        public static string FunctionName = "ExtractBookInfoFromImage";

        public static ChatTool Build()
        {
            return ChatTool.CreateFunctionTool(
                functionName: FunctionName,
                functionDescription: "Извлекает структурированную информацию о книге из изображения обложки",
                functionParameters: BinaryData.FromBytes("""
                {
                    "type": "object",
                    "properties": {
                        "image_base64": {
                            "type": "string",
                            "description": "Изображение обложки книги в формате base64"
                        },
                        "image_mime_type": {
                            "type": "string",
                            "enum": ["image/jpeg", "image/png", "image/webp"],
                            "description": "MIME-тип изображения"
                        },
                        "additional_prompt": {
                            "type": "string",
                            "description": "Дополнительные инструкции для анализа изображения (опционально)"
                        }
                    },
                    "required": ["image_base64", "image_mime_type"]
                }
                """u8.ToArray())
            );
        }
    }
}
