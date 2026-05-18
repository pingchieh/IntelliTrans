using OpenAI.Chat;

namespace IntelliTrans.Cli;

internal static class Prompts
{
    public static List<ChatMessage> CreateTranslatePrompt(string language, string originalText)
    {
        return
        [
            new SystemChatMessage(
                $"你是一名专业的.Net软件工程师，你熟悉 C#/.Net 的各种专业术语，现在你需要将Microsoft .NET SDK IntelliSense的文档翻译为{language}。"
            ),
            new SystemChatMessage(
                $$"""
                将以下 XML 内容翻译为{{language}}，确保严格遵循以下要求：

                ### 翻译要求：
                - **目标语言**：{{language}}。
                - **意义准确**：确保翻译准确传达原文含义，避免任何歧义或误解。
                - **专业术语**：使用准确的专业术语，确保技术文档的专业性。

                ### 格式要求：
                - 确保翻译后的 Xml 结构与原文一致，例如标签和属性（如 `<see cref="T:System.Type"/>`）。
                - 使用`{ }`包裹的内容保持不变。
                - 使用标签包裹的内容，例如`<c> </c>`包裹的内容保持不变。

                ### 输入结构：
                - 使用Markdown的代码块包裹起来的Xml字符串。

                ### 输出结构：
                - 将翻译的结果使用Markdown的代码块包裹起来。

                ### 翻译步骤：
                1. 仔细阅读原文，理解文本的上下文和技术含义。
                2. 仅翻译 XML 标签之间的文本内容，保持标签和属性不变。
                3. 确保翻译后的文本流畅、准确，符合{{language}}表达习惯。
                4. 确保翻译后的文本符合Xml规范，不会引发错误。
                5. 仅输出用代码块包裹翻译结果，不要添加任何其它内容。
                """
            ),
            new UserChatMessage(
                """
                ```xml
                The <see cref="T:System.Type"/> that indicates where this operation is used.
                ```
                """
            ),
            new AssistantChatMessage(
                """
                ```xml
                指示此操作所使用的<see cref="T:System.Type"/>。
                ```
                """
            ),
            new UserChatMessage(
                """
                ```xml
                The entity type '{entityType}' is mapped to the 'DbFunction' named '{functionName}' with return type '{returnType}'. Ensure that the mapped function returns 'IQueryable&lt;{clrType}&gt;'
                ```
                """
            ),
            new AssistantChatMessage(
                """
                ```xml
                实体类型'{entityType}'被映射到名为'{functionName}'的'DbFunction'，返回类型为'{returnType}'。请确保映射的函数返回'IQueryable&lt;{clrType}&gt;'
                ```
                """
            ),
            new UserChatMessage(
                $"""
                ```xml
                {originalText}
                ```
                """
            ),
        ];
    }

    public static List<ChatMessage> CreateOptimizePrompt(
        string language,
        string originalText,
        string translationText
    )
    {
        return
        [
            new SystemChatMessage(
                $"你是一名专业的翻译编辑,精通English和{language}，擅长将技术文档翻译成自然流畅的{language}。"
            ),
            new SystemChatMessage(
                $$"""
                下面是Microsoft .NET SDK IntelliSense的XML文档词条的一部分原文和其初始翻译,请根据你的专业知识对翻译进行优化。

                按照以下要求对翻译进行优化：

                ### 格式要求：
                - 确保翻译后的 Xml 结构与原文一致，例如标签和属性（如 `<see cref="T:System.Type"/>`）。
                - 使用`{ }`包裹的内容保持不变。
                - 使用标签包裹的内容，例如`<c> </c>`包裹的内容保持不变。

                ### 步骤：
                1. 仔细阅读原文和初始翻译，确认翻译的语义与原文一致。
                2. 翻译文本的遣词造句要专业流畅,没有机翻的生硬感。
                3. 确保翻译后的文本格式正确,符合上面的格式要求。
                4. 以上步骤可重复多次，直到翻译质量达到最佳。

                ### 输出要求：
                将优化后的翻译放在xml代码块中，不包含其它内容。
                """
            ),
            new UserChatMessage(
                """
                原文：
                ```xml
                Converts instances of <see cref="T:System.Windows.Input.InputScopeName" /> to and from other data types.
                ```

                初始翻译：
                ```xml
                将<see cref="T:System.Windows.Input.InputScopeName" />的实例转换为其他数据类型，或将其他数据类型转换为其实例。
                ```
                """
            ),
            new AssistantChatMessage(
                """
                ```xml
                在<see cref="T:System.Windows.Input.InputScopeName" />实例与其他数据类型之间进行双向转换。
                ```
                """
            ),
            new UserChatMessage(
                $"""
                原文：
                ```xml
                {originalText}
                ```

                初始翻译：
                ```xml
                {translationText}
                ```
                """
            ),
        ];
    }
}
