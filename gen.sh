#!/bin/bash
TOOL_DIR="./Tools/ProtoGenTool.dll"
PROTOC="./Tools/protoc/mac/protoc"
PROTO_FILES="./Proto"
PROTO_GEN="./CSharp/ProtoGen"

SETTING_FILE="./appsettings.json"

dotnet $TOOL_DIR $SETTING_FILE

function getproto(){
    for element in `ls $1`
    do  
        dir_or_file=$1"/"$element
        if [ -d $dir_or_file ]
        then 
            getproto $dir_or_file
        else
            $PROTOC --csharp_out $PROTO_GEN $1"/"$element
        fi  
    done
}

rm -rf $PROTO_GEN
mkdir $PROTO_GEN
getproto $PROTO_FILES

echo "Generate protobuf finish!"