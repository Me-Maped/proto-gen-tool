// 修改后的CSharp.txt模板
// 该文件自动生成，请勿手动修改!!!
// 生成时间: {{generate_time}}
// - by Maped
namespace {{pb_namespace}}
{
    // 协议号定义
    public enum {{type_file_name}}
    {
        {{for info in infos}}
        {{info.protocol_name}} = {{info.protocol_id}},
        {{end}}
    }

    // 协议类型映射
    public static class {{type_file_name}}Helper
    {
        private static readonly Dictionary<int, Type> _typeMap = new Dictionary<int, Type>
        {
            {{for info in infos}}
            { {{info.protocol_id}}, typeof({{info.protocol_name}}) },
            {{end}}
        };

        public static Type GetType(int protocolId)
        {
            return _typeMap.TryGetValue(protocolId, out var type) ? type : null;
        }
    }
}