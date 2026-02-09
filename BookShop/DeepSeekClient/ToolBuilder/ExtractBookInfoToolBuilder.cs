using OpenAI.Chat;

namespace DeepSeek.ToolBuilder
{
    public static class ExtractBookInfoToolBuilder
    {
        public static ChatTool Build()
        {
            return ChatTool.CreateFunctionTool(
                functionName: "ExtractBookInfo",
                functionDescription: "Извлекает структурированную информацию о книге из текстового описания",
                functionParameters: BinaryData.FromBytes("""
                    {
                        "type": "object",
                        "properties": {
                            "title": {
                                "type": "string",
                                "description": "Название книги"
                            },
                            "author": {
                                "type": "string",
                                "description": "Автор(ы) книги"
                            },
                            "publishingHouse": {
                                "type": "string",
                                "description": "Издательство"
                            },
                            "year": {
                                "type": "integer",
                                "description": "Год издания"
                            },
                            "isbn": {
                                "type": "string",
                                "description": "ISBN код"
                            },
                            "confidence": {
                                "type": "number",
                                "description": "Уверенность в распознавании (0.0-1.0)"
                            },
                            "language": {
                                "type": "string",
                                "description": "Язык книги"
                            }
                        },
                        "required": ["title"]
                    }
                    """u8.ToArray())
            );
        }

    }
}
