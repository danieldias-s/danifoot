import 'dart:io';
import 'package:analyzer/dart/analysis/utilities.dart';
import 'package:analyzer/dart/ast/ast.dart';
import 'package:analyzer/dart/ast/visitor.dart';

class Migration extends RecursiveAstVisitor<void> {
  final String source;
  final Map<int,(int,String)> edits={};
  final List<(int,int)> removed=[];
  Migration(this.source);
  void replace(AstNode node,String value) {
    if(removed.any((r)=>node.offset>=r.$1&&node.end<=r.$2))return;
    edits[node.offset]=(node.length,value);
    for(AstNode? p=node.parent;p!=null;p=p.parent) {
      if(p is InstanceCreationExpression && p.keyword?.lexeme=='const')edits[p.keyword!.offset]=(p.keyword!.length,'');
      if(p is TypedLiteral && p.constKeyword!=null)edits[p.constKeyword!.offset]=(p.constKeyword!.length,'');
    }
  }
  String role(AstNode node,String fallback) {
    for(AstNode? p=node.parent;p!=null;p=p.parent) {
      if(p is InstanceCreationExpression || p is MethodInvocation) {
        final type=p is InstanceCreationExpression?p.constructorName.type.toSource():(p as MethodInvocation).methodName.name;
        if(type=='TextStyle'||type=='Icon')return fallback;
        if(type=='BorderSide'||type=='Border')return 'border';
        if(['BoxDecoration','Container','ColoredBox'].contains(type))return fallback=='accent'?'selectedBackground':fallback;
      }
    }
    return fallback;
  }
  static const names={'_gold':'accent','_green':'surface','_panelGreen':'surface','_rowEvenGreen':'tableRow',
    '_rowOddGreen':'tableRowAlternate','_rowTopGreen':'surfaceAlternate','_borderGreen':'border','_mutedText':'textSecondary','_headingStyle':'headingStyle'};
  @override void visitTopLevelVariableDeclaration(TopLevelVariableDeclaration node) {
    if(node.variables.variables.any((v)=>names.containsKey(v.name.lexeme))) {
      removed.add((node.offset,node.end));edits[node.offset]=(node.length,'');return;
    }
    super.visitTopLevelVariableDeclaration(node);
  }
  @override void visitSimpleIdentifier(SimpleIdentifier node) {
    if(names.containsKey(node.name))replace(node,'ClassicTheme.of(context).${role(node,names[node.name]!)}');
    super.visitSimpleIdentifier(node);
  }
  @override void visitInstanceCreationExpression(InstanceCreationExpression node) {
    if(node.constructorName.type.toSource()=='Color') {
      final m=RegExp(r'0x([0-9A-Fa-f]{8})').firstMatch(node.toSource());
      const colors={'FFFFD700':'accent','FF3E8450':'border','FF062E0F':'surface','FF0E3B19':'surface',
        'FF0B3515':'tableRow','FF10421C':'tableRowAlternate','FF185728':'surfaceAlternate','FF174F26':'surfaceAlternate',
        'FF205B30':'border','FF0A4D1C':'background','FF136F2D':'buttonBackground','FF00E676':'accent',
        'FF0D3B18':'surface','FF093915':'surface','FF124E21':'surfaceAlternate','FF145224':'buttonBackground',
        'FF1B5E20':'surfaceAlternate','FF073612':'surface','FF144B22':'surfaceAlternate','FF1A3A20':'surfaceAlternate','FF164D24':'surfaceAlternate','FF0A4218':'surfaceAlternate',
        'FFD2EBD6':'textSecondary','FFC0C0C0':'buttonBackground','FF145B28':'surfaceAlternate'};
      final token=colors[m?.group(1)?.toUpperCase()];
      if(token!=null){replace(node,'ClassicTheme.of(context).${role(node,token)}');return;}
    }
    super.visitInstanceCreationExpression(node);
  }
  @override void visitMethodInvocation(MethodInvocation node) {
    if(node.methodName.name=='Color') {
      final m=RegExp(r'0x([0-9A-Fa-f]{8})').firstMatch(node.toSource());
      const colors={'FFFFD700':'accent','FF3E8450':'border','FF062E0F':'surface','FF0E3B19':'surface',
        'FF0B3515':'tableRow','FF10421C':'tableRowAlternate','FF185728':'surfaceAlternate','FF174F26':'surfaceAlternate',
        'FF205B30':'border','FF0A4D1C':'background','FF136F2D':'buttonBackground','FF00E676':'accent',
        'FF0D3B18':'surface','FF093915':'surface','FF124E21':'surfaceAlternate','FF145224':'buttonBackground',
        'FF1B5E20':'surfaceAlternate','FF073612':'surface','FF144B22':'surfaceAlternate','FF1A3A20':'surfaceAlternate','FF164D24':'surfaceAlternate','FF0A4218':'surfaceAlternate',
        'FFD2EBD6':'textSecondary','FFC0C0C0':'buttonBackground','FF145B28':'surfaceAlternate'};
      final token=colors[m?.group(1)?.toUpperCase()];
      if(token!=null){replace(node,'ClassicTheme.of(context).${role(node,token)}');return;}
    }
    super.visitMethodInvocation(node);
  }
  @override void visitPropertyAccess(PropertyAccess node) {
    if(node.toSource()=='ClassicTheme.of(context).accent') {
      final token=role(node,'accent');
      if(token!='accent')replace(node,'ClassicTheme.of(context).$token');
    }
    super.visitPropertyAccess(node);
  }
  @override void visitPrefixedIdentifier(PrefixedIdentifier node) {
    if(node.prefix.name=='Colors') {
      if(node.identifier.name=='black') {
        var text=false,background=false;
        for(AstNode? p=node.parent;p!=null;p=p.parent){
          if(p is InstanceCreationExpression || p is MethodInvocation){
            final type=p is InstanceCreationExpression?p.constructorName.type.toSource():(p as MethodInvocation).methodName.name;
            if(type=='TextStyle'||type=='Icon'){text=true;break;}
            if(type=='BoxDecoration'||type=='Container'||type=='ColoredBox'){background=true;break;}
          }
        }
        if(text||background){replace(node,'ClassicTheme.of(context).${text?'selectedText':'surface'}');return;}
      }

      final token={'white':'textPrimary','white70':'textSecondary','white60':'textSecondary','white54':'textSecondary' }[node.identifier.name];
      if(token!=null){replace(node,'ClassicTheme.of(context).$token');return;}
    }
    super.visitPrefixedIdentifier(node);
  }
  String apply() {
    var result=source;
    final positions=edits.keys.toList()..sort((a,b)=>b.compareTo(a));
    for(final p in positions){final e=edits[p]!;result=result.replaceRange(p,p+e.$1,e.$2);}
    return result;
  }
}
void main() {
  for(final name in ['main.dart','shell_actions.dart','player_rankings.dart','classic_match_board.dart','custom_assembly_editor.dart','world_creator.dart']) {
    final file=File('client/elifoot_flutter/lib/$name');var source=file.readAsStringSync();
    final visitor=Migration(source);parseString(content:source,throwIfDiagnostics:false).unit.accept(visitor);source=visitor.apply();
    if(name!='shell_actions.dart'&&!source.contains("import 'club_theme.dart';"))source="import 'club_theme.dart';\n$source";
    file.writeAsStringSync(source);stdout.writeln('$name: ${visitor.edits.length} token/const edits');
  }
}
