import 'package:flutter/material.dart';
import 'player_rankings.dart' show formatClassicMoney;
import 'club_crest.dart';
import 'club_theme.dart';
import 'creator_visual.dart';
import 'visual_components.dart';

class ClubSelectionDialog extends StatefulWidget {
  final List<Map<String, dynamic>> clubs;
  const ClubSelectionDialog({super.key, required this.clubs});

  @override
  State<ClubSelectionDialog> createState() => _ClubSelectionDialogState();
}

class _ClubSelectionDialogState extends State<ClubSelectionDialog> {
  final _scroll = ScrollController();

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context);
    return LayoutBuilder(
      builder: (context, constraints) {
        final mobile = constraints.maxWidth < 600;
        return Stack(
          children: [
            Positioned.fill(
              child: IgnorePointer(
                child: CreatorBackdrop(
                  child: ColoredBox(color: Colors.black.withValues(alpha: .18)),
                ),
              ),
            ),
            if (!mobile)
              const Positioned(
                top: 10,
                left: 0,
                right: 0,
                child: IgnorePointer(
                  child: Center(child: CreatorBrand(size: 30)),
                ),
              ),
            Dialog(
              key: const Key('club-selection-dialog'),
              insetPadding: EdgeInsets.symmetric(
                horizontal: mobile ? 10 : 28,
                vertical: mobile ? 14 : 28,
              ),
              backgroundColor: theme.primary,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12),
                side: BorderSide(color: theme.paper.border, width: 1.5),
              ),
              clipBehavior: Clip.antiAlias,
              child: SafeArea(
                child: SizedBox(
                  width: 940,
                  height: constraints.maxHeight * (mobile ? .94 : .86),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Padding(
                        padding: EdgeInsets.symmetric(
                          horizontal: mobile ? 18 : 28,
                          vertical: mobile ? 18 : 22,
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            if (mobile) ...[
                              const FittedBox(
                                fit: BoxFit.scaleDown,
                                child: CreatorBrand(size: 25),
                              ),
                              const SizedBox(height: 22),
                            ],
                            Semantics(
                              namesRoute: true,
                              child: Text(
                                'SELECIONE SEU CLUBE',
                                style: TextStyle(
                                  color: theme.selectedBackground,
                                  fontSize: mobile ? 27 : 32,
                                  fontWeight: FontWeight.w700,
                                  height: 1.1,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                      Expanded(
                        child: ClassicPaper(
                          builder: (context) {
                            final paper = ClassicTheme.of(context);
                            return ColoredBox(
                              color: mobile ? theme.primary : paper.surface,
                              child: Scrollbar(
                                controller: _scroll,
                                thumbVisibility: true,
                                child: ListView.separated(
                                  key: const Key('club-selection-list'),
                                  controller: _scroll,
                                  padding: EdgeInsets.symmetric(
                                    horizontal: mobile ? 8 : 14,
                                    vertical: mobile ? 2 : 8,
                                  ),
                                  itemCount: widget.clubs.length,
                                  separatorBuilder: (_, index) => mobile
                                      ? const SizedBox(height: 8)
                                      : Divider(height: 1, color: paper.border),
                                  itemBuilder: (context, index) {
                                    final club = widget.clubs[index];
                                    final palette = club['colorPalette'];
                                    final selectedPalette = palette is Map
                                        ? Map<String, dynamic>.from(palette)
                                        : <String, dynamic>{
                                            'primaryColor':
                                                club['primaryColorHex'],
                                            'secondaryColor':
                                                club['secondaryColorHex'],
                                          };
                                    void assume() => Navigator.pop(
                                      context,
                                      club['id'].toString(),
                                    );
                                    return Material(
                                      color: paper.surface,
                                      shape: RoundedRectangleBorder(
                                        borderRadius: BorderRadius.circular(
                                          mobile ? 9 : 0,
                                        ),
                                      ),
                                      clipBehavior: Clip.antiAlias,
                                      child: InkWell(
                                        key: Key('choose-club-${club['id']}'),
                                        onTap: assume,
                                        child: Padding(
                                          padding: EdgeInsets.symmetric(
                                            horizontal: mobile ? 10 : 18,
                                            vertical: mobile ? 12 : 13,
                                          ),
                                          child: Row(
                                            children: [
                                              ClubCrest(
                                                club: club['name'].toString(),
                                                palette: selectedPalette,
                                                size: mobile ? 43 : 49,
                                              ),
                                              const SizedBox(width: 12),
                                              Expanded(
                                                child: Column(
                                                  crossAxisAlignment:
                                                      CrossAxisAlignment.start,
                                                  children: [
                                                    Text(
                                                      club['name'].toString(),
                                                      maxLines: 2,
                                                      overflow:
                                                          TextOverflow.ellipsis,
                                                      style: TextStyle(
                                                        color:
                                                            paper.textPrimary,
                                                        fontSize: mobile
                                                            ? 20
                                                            : 24,
                                                        fontWeight:
                                                            FontWeight.w700,
                                                        height: 1.05,
                                                      ),
                                                    ),
                                                    const SizedBox(height: 5),
                                                    Text(
                                                      '${club['divisionDisplay']} • Caixa ${formatClassicMoney((club['cashBalance'] as num?) ?? 0)}',
                                                      style: TextStyle(
                                                        color:
                                                            paper.textPrimary,
                                                        fontSize: mobile
                                                            ? 15
                                                            : 18,
                                                        height: 1.1,
                                                      ),
                                                    ),
                                                  ],
                                                ),
                                              ),
                                              const SizedBox(width: 10),
                                              OutlinedButton(
                                                onPressed: assume,
                                                style: OutlinedButton.styleFrom(
                                                  backgroundColor:
                                                      theme.primary,
                                                  foregroundColor: readableOn(
                                                    theme.selectedBackground,
                                                    theme.primary,
                                                  ),
                                                  side: BorderSide(
                                                    color: theme
                                                        .selectedBackground,
                                                    width: 1.5,
                                                  ),
                                                  minimumSize: Size(
                                                    mobile ? 82 : 132,
                                                    44,
                                                  ),
                                                  padding: EdgeInsets.symmetric(
                                                    horizontal: mobile ? 9 : 22,
                                                  ),
                                                  shape: RoundedRectangleBorder(
                                                    borderRadius:
                                                        BorderRadius.circular(
                                                          4,
                                                        ),
                                                  ),
                                                ),
                                                child: Text(
                                                  'ASSUMIR',
                                                  style: TextStyle(
                                                    fontSize: mobile ? 19 : 23,
                                                    fontWeight: FontWeight.w700,
                                                  ),
                                                ),
                                              ),
                                            ],
                                          ),
                                        ),
                                      ),
                                    );
                                  },
                                ),
                              ),
                            );
                          },
                        ),
                      ),
                      Padding(
                        padding: EdgeInsets.all(mobile ? 14 : 18),
                        child: Align(
                          alignment: mobile
                              ? Alignment.center
                              : Alignment.centerRight,
                          child: SizedBox(
                            width: mobile ? double.infinity : 170,
                            height: 46,
                            child: OutlinedButton(
                              onPressed: () => Navigator.pop(context),
                              style: OutlinedButton.styleFrom(
                                foregroundColor: theme.selectedBackground,
                                side: BorderSide(
                                  color: mobile
                                      ? theme.selectedBackground
                                      : Colors.transparent,
                                ),
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(6),
                                ),
                              ),
                              child: const Text(
                                'CANCELAR',
                                style: TextStyle(
                                  fontSize: 22,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}
